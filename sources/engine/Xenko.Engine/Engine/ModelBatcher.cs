using System.Collections.Generic;
using System.Text;
using Xenko.Engine;
using Xenko.Core.Serialization.Contents;
using Xenko.Rendering;
using Xenko.Graphics;
using Xenko.Core.Serialization;
using Xenko.Graphics.Data;
using System;
using Xenko.Extensions;
using Xenko.Core.Mathematics;
using System.Linq;
using System.Threading.Tasks;
using Xenko.Rendering.Materials;
using Xenko.Core;
using System.Runtime.InteropServices;
using Xenko.Rendering.Rendering;

namespace Xenko.Engine
{
    /// <summary>
    /// System for batching entities and models together, to reduce draw calls and entity processing overhead. Works great with static geometry.
    /// </summary>
    public class ModelBatcher
    {
        private struct BatchingChunk
        {
            public Entity Entity;
            public Model Model;
            public Matrix? Transform;
            public int MaterialIndex;
        }

        /// <summary>
        /// Unpacks a raw buffer of vertex data into proper arrays
        /// </summary>
        /// <returns>Returns true if some data was successful in extraction</returns>
        public static unsafe bool UnpackRawVertData(byte[] data, VertexDeclaration declaration,
                                                    out Vector3[] positions, out Vector3[] normals,
                                                    out Vector2[] uvs, out Color4[] colors)
        {
            positions = null;
            normals = null;
            uvs = null;
            colors = null;
            if (data == null || declaration == null || data.Length <= 0) return false;
            VertexElement[] elements = declaration.VertexElements;
            int totalEntries = data.Length / declaration.VertexStride;
            positions = new Vector3[totalEntries];
            int[] eoffsets = new int[elements.Length];
            for (int i = 1; i < elements.Length; i++) eoffsets[i] = eoffsets[i - 1] + elements[i - 1].Format.SizeInBytes();
            fixed (byte* dp = data)
            {
                for (int offset = 0; offset < data.Length; offset += declaration.VertexStride)
                {
                    int vertindex = offset / declaration.VertexStride;
                    for (int i = 0; i < elements.Length; i++)
                    {
                        VertexElement e = elements[i];
                        switch (e.SemanticName)
                        {
                            case "POSITION":
                                positions[vertindex] = *(Vector3*)&dp[offset + eoffsets[i]];
                                break;
                            case "NORMAL":
                                if (normals == null) normals = new Vector3[totalEntries];
                                normals[vertindex] = *(Vector3*)&dp[offset + eoffsets[i]];
                                break;
                            case "COLOR":
                                if (colors == null) colors = new Color4[totalEntries];
                                colors[vertindex] = *(Color4*)&dp[offset + eoffsets[i]];
                                break;
                            case "TEXCOORD":
                                if (uvs == null) uvs = new Vector2[totalEntries];
                                uvs[vertindex] = *(Vector2*)&dp[offset + eoffsets[i]];
                                break;
                        }
                    }
                }
            }
            return true;
        }

        private static unsafe void ProcessMaterial(List<BatchingChunk> chunks, MaterialInstance material, Model prefabModel, HashSet<Entity> unbatched = null)
        {
            //actually create the mesh
            List<VertexPositionNormalTexture> vertsNT = null;
            List<VertexPositionNormalColor> vertsNC = null;
            List<uint> indiciesList = new List<uint>();
            BoundingBox bb = new BoundingBox(new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity),
                                             new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity));
            uint indexOffset = 0;
            for (int i = 0; i < chunks.Count; i++)
            {
                BatchingChunk chunk = chunks[i];
                if (unbatched != null && unbatched.Contains(chunk.Entity)) continue; // don't try batching other things in this entity if some failed
                if (chunk.Entity != null) chunk.Entity.Transform.UpdateWorldMatrix();
                for (int j = 0; j < chunk.Model.Meshes.Count; j++)
                {
                    Mesh modelMesh = chunk.Model.Meshes[j];
                    //process only right material
                    if (modelMesh.MaterialIndex == chunk.MaterialIndex)
                    {
                        Vector3[] positions = null, normals = null;
                        Vector2[] uvs = null;
                        Color4[] colors = null;

                        //vertexes
                        if (modelMesh.Draw is StagedMeshDraw)
                        {
                            StagedMeshDraw smd = modelMesh.Draw as StagedMeshDraw;

                            object verts = smd.Verticies;

                            if (verts is VertexPositionNormalColor[])
                            {
                                VertexPositionNormalColor[] vpnc = verts as VertexPositionNormalColor[];
                                positions = new Vector3[vpnc.Length];
                                normals = new Vector3[vpnc.Length];
                                colors = new Color4[vpnc.Length];
                                for (int k = 0; k < vpnc.Length; k++)
                                {
                                    positions[k] = vpnc[k].Position;
                                    normals[k] = vpnc[k].Normal;
                                    colors[k] = vpnc[k].Color;
                                }
                            }
                            else if (verts is VertexPositionNormalTexture[])
                            {
                                VertexPositionNormalTexture[] vpnc = verts as VertexPositionNormalTexture[];
                                positions = new Vector3[vpnc.Length];
                                normals = new Vector3[vpnc.Length];
                                uvs = new Vector2[vpnc.Length];
                                for (int k = 0; k < vpnc.Length; k++)
                                {
                                    positions[k] = vpnc[k].Position;
                                    normals[k] = vpnc[k].Normal;
                                    uvs[k] = vpnc[k].TextureCoordinate;
                                }
                            }
                            else
                            {
                                // unsupported StagedMeshDraw
                                if (unbatched != null) unbatched.Add(chunk.Entity);
                                continue;
                            }

                            // take care of indicies
                            for (int k = 0; k < smd.Indicies.Length; k++) indiciesList.Add(smd.Indicies[k] + indexOffset);
                        }
                        else
                        {
                            Xenko.Graphics.Buffer buf = modelMesh.Draw?.VertexBuffers[0].Buffer;
                            Xenko.Graphics.Buffer ibuf = modelMesh.Draw?.IndexBuffer.Buffer;
                            if (buf == null || buf.VertIndexData == null ||
                                ibuf == null || ibuf.VertIndexData == null)
                            {
                                if (unbatched != null) unbatched.Add(chunk.Entity);
                                continue;
                            }

                            if (UnpackRawVertData(buf.VertIndexData, modelMesh.Draw.VertexBuffers[0].Declaration,
                                                  out positions, out normals, out uvs, out colors) == false)
                            {
                                if (unbatched != null) unbatched.Add(chunk.Entity);
                                continue;
                            }

                            if (indiciesList == null) indiciesList = new List<uint>();

                            // indicies
                            fixed (byte* pdst = ibuf.VertIndexData)
                            {
                                if (modelMesh.Draw.IndexBuffer.Is32Bit)
                                {
                                    var dst = (uint*)pdst;

                                    int numIndices = ibuf.VertIndexData.Length / sizeof(uint);
                                    for (var k = 0; k < numIndices; k++)
                                    {
                                        // Offset indices
                                        indiciesList.Add(dst[k] + indexOffset);
                                    }
                                }
                                else
                                {
                                    var dst = (ushort*)pdst;

                                    int numIndices = ibuf.VertIndexData.Length / sizeof(ushort);
                                    for (var k = 0; k < numIndices; k++)
                                    {
                                        // Offset indices
                                        indiciesList.Add(dst[k] + indexOffset);
                                    }
                                }
                            }
                        }

                        // what kind of structure will we be making, if we haven't picked one already?
                        if (vertsNT == null && vertsNC == null)
                        {
                            if (uvs != null)
                            {
                                vertsNT = new List<VertexPositionNormalTexture>();
                            }
                            else
                            {
                                vertsNC = new List<VertexPositionNormalColor>();
                            }
                        }

                        // transform verts/norms by matrix
                        Matrix worldMatrix = chunk.Entity == null ? (chunk.Transform ?? Matrix.Identity) : chunk.Entity.Transform.WorldMatrix;
                        bool matrixNeeded = worldMatrix != Matrix.Identity;
                        for (int k = 0; k < positions.Length; k++)
                        {
                            if (matrixNeeded) Vector3.Transform(ref positions[k], ref worldMatrix, out positions[k]);

                            // update bounding box?
                            Vector3 pos = positions[k];
                            if (pos.X > bb.Maximum.X) bb.Maximum.X = pos.X;
                            if (pos.Y > bb.Maximum.Y) bb.Maximum.Y = pos.Y;
                            if (pos.Z > bb.Maximum.Z) bb.Maximum.Z = pos.Z;
                            if (pos.X < bb.Minimum.X) bb.Minimum.X = pos.X;
                            if (pos.Y < bb.Minimum.Y) bb.Minimum.Y = pos.Y;
                            if (pos.Z < bb.Minimum.Z) bb.Minimum.Z = pos.Z;

                            if (normals != null && matrixNeeded) Vector3.TransformNormal(ref normals[k], ref worldMatrix, out normals[k]);

                            if (vertsNT != null)
                            { 
                                vertsNT.Add(new VertexPositionNormalTexture
                                {
                                    Position = positions[k],
                                    Normal = normals != null ? normals[k] : Vector3.UnitY,
                                    TextureCoordinate = uvs[k]
                                });
                            }
                            else
                            {
                                vertsNC.Add(new VertexPositionNormalColor
                                {
                                    Position = positions[k],
                                    Normal = normals != null ? normals[k] : Vector3.UnitY,
                                    Color = colors != null ? colors[k] : Color4.White
                                });
                            }
                        }

                        indexOffset += (uint)positions.Length;
                    }
                }
            }

            if (indiciesList.Count <= 0) return;

            uint[] indicies = indiciesList.ToArray();

            // make stagedmesh with verts
            StagedMeshDraw md;
            if (vertsNT != null)
            {
                md = StagedMeshDraw.MakeStagedMeshDraw<VertexPositionNormalTexture>(indicies, vertsNT.ToArray(), VertexPositionNormalTexture.Layout);
            }
            else if (vertsNC != null)
            {
                md = StagedMeshDraw.MakeStagedMeshDraw<VertexPositionNormalColor>(indicies, vertsNC.ToArray(), VertexPositionNormalColor.Layout);
            }
            else return;

            Mesh m = new Mesh
            {
                Draw = md,
                BoundingBox = bb,
                MaterialIndex = prefabModel.Materials.Count
            };

            prefabModel.Add(m);
            prefabModel.Add(material);
        }

        private static void Gather(Entity e, List<Entity> into)
        {
            into.Add(e);
            foreach (Entity ec in e.GetChildren()) Gather(ec, into);
        }

        /// <summary>
        /// Takes an Entity tree and does its best to batch itself and children into one entity. Automatically removes batched entities.
        /// </summary>
        /// <param name="root">The root entity to batch from and merge into</param>
        /// <returns>Returns the number of successfully batched and removed entities</returns>
        public static int BatchChildren(Entity root)
        {
            // gather all of the children (and root)
            List<Entity> allEs = new List<Entity>();
            Gather(root, allEs);
            // capture the original transform of the root, then clear it
            // so it isn't included in individual verticies
            Vector3 originalPosition = root.Transform.Position;
            Quaternion originalRotation = root.Transform.Rotation;
            Vector3 originalScale = root.Transform.Scale;
            root.Transform.Position = Vector3.Zero;
            root.Transform.Scale = Vector3.One;
            root.Transform.Rotation = Quaternion.Identity;
            // batch them all together into one model
            Model m = BatchEntities(allEs, out HashSet<Entity> unbatched);
            // set the root to use the new batched model
            root.GetOrCreate<ModelComponent>().Model = m;
            // restore the root transform
            root.Transform.Rotation = originalRotation;
            root.Transform.Scale = originalScale;
            root.Transform.Position = originalPosition;
            // we will want to remove entities from the scene that were batched,
            // so convert allEs into a list of things we want to remove
            foreach (Entity skipped in unbatched) allEs.Remove(skipped);
            // remove now batched entities from the scene, skipping root
            for (int i = 0; i < allEs.Count; i++)
            {
                if (allEs[i] != root) allEs[i].Scene = null;
            }
            // return how many things we were able to batch
            return allEs.Count;
        }
        
        private static bool ModelOKForBatching(Model model)
        {
            if (model == null) return false;
            for (int i = 0; i < model.Meshes.Count; i++)
            {
                Mesh m = model.Meshes[i];
                if (m.Draw.PrimitiveType != PrimitiveType.TriangleList ||
                    m.Draw.VertexBuffers == null && m.Draw is StagedMeshDraw == false ||
                    m.Draw.VertexBuffers != null && m.Draw.VertexBuffers.Length != 1) return false;
            }
            return true;
        }

        /// <summary>
        /// Generate a batched model. Copies the model to all positions in listOfTransforms into one batched model.
        /// </summary>
        /// <param name="model">Model to copy around</param>
        /// <param name="listOfTransforms">List of transforms to place the model</param>
        /// <returns>Returns batched model. Null if model coouldn't be made, like if buffers for meshes couldn't be found</returns>
        public static Model GenerateBatch(Model model, List<Matrix> listOfTransforms)
        {
            if (ModelOKForBatching(model) == false) return null;

            var materials = new Dictionary<MaterialInstance, List<BatchingChunk>>();

            for (var index = 0; index < model.Materials.Count; index++)
            {
                var material = model.Materials[index];

                for (int i = 0; i < listOfTransforms.Count; i++)
                {
                    var chunk = new BatchingChunk { Entity = null, Model = model, MaterialIndex = index, Transform = listOfTransforms[i] };

                    if (materials.TryGetValue(material, out var entities))
                    {
                        entities.Add(chunk);
                    }
                    else
                    {
                        materials[material] = new List<BatchingChunk> { chunk };
                    }
                }
            }

            Model prefabModel = new Model();

            foreach (var material in materials)
            {
                ProcessMaterial(material.Value, material.Key, prefabModel);
            }

            prefabModel.UpdateBoundingBox();

            return prefabModel;
        }

        /// <summary>
        /// Batches a model by trying to combine meshes and materials in the model
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public static Model BatchModel(Model model)
        {
            if (ModelOKForBatching(model) == false) return model;

            var materials = new Dictionary<MaterialInstance, List<BatchingChunk>>();

            for (var index = 0; index < model.Materials.Count; index++)
            {
                var material = model.Materials[index];

                var chunk = new BatchingChunk { Entity = null, Model = model, MaterialIndex = index, Transform = null };

                if (materials.TryGetValue(material, out var entities))
                {
                    entities.Add(chunk);
                }
                else
                {
                    materials[material] = new List<BatchingChunk> { chunk };
                }
            }

            Model prefabModel = new Model();

            foreach (var material in materials)
            {
                ProcessMaterial(material.Value, material.Key, prefabModel);
            }

            prefabModel.UpdateBoundingBox();

            return prefabModel;
        }

        /// <summary>
        /// Returns a model that batches as much as possible from all of the entities in the list. Any entities that couldn't be batched into
        /// the model will be added to unbatched. Entities may not get batched if underlying buffer data couldn't be found to batch with
        /// </summary>
        /// <param name="entityList">List of entities to be batched</param>
        /// <param name="unbatched">List of entities that failed to batch</param>
        /// <returns>Model with meshes merged as much as possible</returns>
        public static Model BatchEntities(List<Entity> entityList, out HashSet<Entity> unbatched)
        {
            var prefabModel = new Model();

            //The objective is to create 1 mesh per material/shadow params
            //1. We group by materials
            //2. Create a mesh per material (might need still more meshes if 16bit indexes or more then 32bit)

            var materials = new Dictionary<MaterialInstance, List<BatchingChunk>>();

            // keep track of any entities that couldn't be batched
            unbatched = new HashSet<Entity>();

            foreach (var subEntity in entityList)
            {
                var modelComponent = subEntity.Get<ModelComponent>();

                if (modelComponent?.Model == null || (modelComponent.Skeleton != null && modelComponent.Skeleton.Nodes.Length != 1) || !modelComponent.Enabled)
                    continue;

                var model = modelComponent.Model;

                if (ModelOKForBatching(model) == false)
                {
                    unbatched.Add(subEntity);
                    continue;
                }

                for (var index = 0; index < model.Materials.Count; index++)
                {
                    var material = model.Materials[index];

                    var chunk = new BatchingChunk { Entity = subEntity, Model = model, MaterialIndex = index };

                    if (materials.TryGetValue(material, out var entities))
                    {
                        entities.Add(chunk);
                    }
                    else
                    {
                        materials[material] = new List<BatchingChunk> { chunk };
                    }
                }
            }

            foreach (var material in materials)
            {
                ProcessMaterial(material.Value, material.Key, prefabModel, unbatched);
            }

            prefabModel.UpdateBoundingBox();

            return prefabModel;
        }
    }
}
