![Focus Engine](https://i.imgur.com/OjANvN9.png)
=======

Welcome to the Focus Engine source code repository!

Focus is an open-source C# game engine for realistic rendering and VR based off of Xenko. You'll still see "Xenko" in many places.
The engine is highly modular and aims at giving game makers more flexibility in their development.
Focus comes with an editor that allows you create and manage the content of your games or applications in a visual and intuitive way.

![Focus Game Studio](https://xenko.com/images/external/script-editor.png)

To learn more about Xenko, visit [xenko.com](https://xenko.com/).

## Why this fork?

My games require the engine to be developed at a faster pace than Xenko. I'm in need of fixes, new features and better performance. These changes will not be supported by the core team, and the absolute most recent changes may not be fully stable. However, you may find them very helpful, and in some cases, essential to projects.

## What is different?

Most of Focus is similar to Xenko and there shouldn't be any loss of functionality over the original. Changes are focused on fixes, performance improvements and new features.

* Virtual Reality: frame rate management, resolution detection, Vulkan support, and automatic UI interaction are some of the VR improvements you'll get "out of the box". Tracking hands is much easier, as you can simply select which hand to track right from GameStudio. Support for multiple forward renderers in VR, with post processing.
* Vulkan: Focus primarily uses Vulkan, which has been significantly overhauled to provide more performance you'd expect from the newer API.
* BepuPhysics2 and Physics: Focus has an additional physics library integrated, which is much faster, multithreaded and pure C#. It isn't integrated with GameStudio though, like Bullet physics is. Look at the Xenko.Physics.Bepu namespace on how to use it. If you decide to still use Bullet, this fork can handle Bullet running in another thread with interpolation.
* API Ease: TransformComponents have nice shortcuts like WorldPosition and WorldRotation. There are also other very useful shortcuts, like Material.Clone to easily clone materials.
* Lots of bugfixes: Lots of issues, and even GameStudio crashes and project corruption, have been fixed/improved in this fork. Some specific examples is crashes when entering invalid data into a color field, particle colors, importing multiple audio files at once, or rendering 3D text from multiple cameras.
* SimpleAudioPool: easily play all sound effects for your whole project from a single object, which handles loading and pooling sound instances automatically. If you use positional sounds, make sure you call UpdatePlayingSoundPositions every frame! See https://github.com/phr00t/FocusEngine/blob/master/sources/engine/Xenko.Engine/Engine/SimpleAudioPool.cs
* CinematicAction: Simple system for performing cinematic actions on objects and calling functions at certain times. Can build a simple timeline for things to move, rotate and execute. See https://github.com/phr00t/FocusEngine/blob/master/sources/engine/Xenko.Engine/Cinematics/CinematicAnimation.cs
* EntityPool: Makes it really easy to reuse entities and prefabs. This can save lots of memory and processing, instead of recreating things that come and go (like enemies or projectiles). See https://github.com/phr00t/FocusEngine/blob/master/sources/engine/Xenko.Engine/Engine/EntityPool.cs
* UI improvements: View frustum is implemented in this fork, so UI elements outside of view won't be drawn for performance reasons. ScrollViewers can work with mouse wheels out of the box. Easily get UI elements to access in code from a Page using GatherUIDictionary. Easily make Lists and Pulldown selection boxes using GridList and PulldownList (not integrated with GameStudio yet, though).
* Better UI Editor: Selecting things in the editor works more intuitively, like hidden things are skipped and smaller things are easier to click.
* UI Text features: vertically align text or use \<color> tags to dynamically change text colors. Use \<br> tags to have multiline text set straight from GameStudio. Need text shadows, outlines or bevels? Precompile a font (right click it in the asset view) that has a Glyph Margin > 0, which will generate a PNG with room to edit in effects right into the glyphs.
* ModelBatcher: Easily make batched models using lots of individual models (think grass and rocks for your whole terrain batched into one draw call and entity). See https://github.com/phr00t/FocusEngine/blob/master/sources/engine/Xenko.Engine/Engine/ModelBatcher.cs
* More Post Processing Effects: Fog and Outline post processing shaders work, out of the box.
* Easy setting game resolution: Game.SetDefaultSettings(width, height, fullscreen) and Game.OverrideDefaultSettings to set and save resolution of your game.
* Easy generating procedural meshes: StagedMeshDraw takes a list of verticies and indicies, no "buffer binding" or "GraphicsDevice" needed. Also will actually upload the mesh when it tries to get rendered automatically, saving time and resources if the mesh doesn't actually ever get viewed.
* Less likely to lose work: files are not actually deleted from GameStudio, just moved to the Recylce Bin.
* Performance: lots of tweaks have been made throughout the engine to maximize performance. This includes reducing locks and enumeration reduction, for example.
* Easy adding/removing entities from the scene: Just do myEntity.Scene = myScene (to add it) or myEntity.Scene = null (to remove it).
* Includes dfkeenan's toolkit designed for this fork (from https://github.com/dfkeenan/XenkoToolkit). May need to add the Toolkit Nuget package to use.
* Takes good things from many different Xenko forks, including the original Xenko branch when it gets updated. May not get everything, like some of the tutorials, sample, non-PC platforms or launcher updates, which I don't maintain.
* Simple binary distribution: No launcher needed. Just download and run the latest release (after making sure you have all of the Visual Studio build prerequisites, see https://github.com/phr00t/FocusEngine/releases.
* Probably lots of other stuff: haven't kept that great of track of improvements, I usually fix things as needed and keep moving forward!

## License

Focus is covered by [MIT](LICENSE.md), unless stated otherwise (i.e. for some files that are copied from other projects).

You can find the list of third party projects [here](THIRD%20PARTY.md).

Contributors need to sign the following [Contribution License Agreement](docs/ContributorLicenseAgreement.md).

## Documentation

Find explanations and information about Xenko:
* [Xenko Manual](https://doc.xenko.com/latest/manual/index.html)
* [API Reference](https://doc.xenko.com/latest/api/index.html)
* [Release Notes](https://doc.xenko.com/latest/ReleaseNotes/index.html)

## Community

Ask for help or report issues:
* [Chat with the community on Discord](https://discord.gg/f6aerfE) [![Join the chat at https://discord.gg/f6aerfE](https://img.shields.io/discord/500285081265635328.svg?style=flat&logo=discord&label=discord)](https://discord.gg/f6aerfE)
* [Discuss topics on our forums](http://forums.xenko.com/)
* [Report engine issues](https://github.com/phr00t/xenko/issues)
* [Donate to support the project](https://www.patreon.com/phr00tssoftware)

## Building from source

### Prerequisites

1. [Git](https://git-scm.com/downloads) (recent version that includes LFS, or install [Git LFS](https://git-lfs.github.com/) separately).
2. [Visual Studio 2019](https://www.visualstudio.com/downloads/) with the following workloads:
  * `.NET desktop development` with `.NET Framework 4.8 targeting pack`
  * `Desktop development with C++` with `Windows 10 SDK (10.0.17763.0)` or later, and `VC++ 2019 version 15.9 v14.16 latest v141 tools` or later (both should be enabled by default)
  * `.NET Core cross-platform development`
  * Optional (to target UWP): `Universal Windows Platform development` with `Windows 10 SDK (10.0.17763.0)`
  * Optional (to target iOS/Android): `Mobile development with .NET` and `Android NDK R13B+` individual component
3. [FBX SDK 2019.0 VS2015](https://www.autodesk.com/developer-network/platform-technologies/fbx-sdk-2019-0)

### Build Focus

1. Clone Focus: `git clone https://github.com/phr00t/FocusEngine.git`
2. Run `<FocusDir>\build\Xenko.PCPlatforms.bat`, which starts Visual Studio 2019, and build.
