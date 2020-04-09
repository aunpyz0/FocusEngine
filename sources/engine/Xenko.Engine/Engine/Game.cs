// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Xenko.Audio;
using Xenko.Core;
using Xenko.Core.Diagnostics;
using Xenko.Core.IO;
using Xenko.Core.Mathematics;
using Xenko.Core.Storage;
using Xenko.Engine;
using Xenko.Engine.Design;
using Xenko.Engine.Processors;
using Xenko.Games;
using Xenko.Graphics;
using Xenko.Graphics.Font;
using Xenko.Graphics.SDL;
using Xenko.Input;
using Xenko.Profiling;
using Xenko.Rendering;
using Xenko.Rendering.Fonts;
using Xenko.Rendering.Sprites;
using Xenko.Shaders.Compiler;
using Xenko.Streaming;
using Xenko.VirtualReality;

namespace Xenko.Engine
{
    /// <summary>
    /// Main Game class system.
    /// </summary>
    public class Game : GameBase, ISceneRendererContext, IGameSettingsService
    {
        /// <summary>
        /// Static event that will be fired when a game is initialized
        /// </summary>
        public static event EventHandler GameStarted;

        /// <summary>
        /// Static event that will be fired when a game is destroyed
        /// </summary>
        public static event EventHandler GameDestroyed;

        private readonly GameFontSystem gameFontSystem;

        private readonly LogListener logListener;

        private DatabaseFileProvider databaseFileProvider;

        /// <summary>
        /// Readonly game settings as defined in the GameSettings asset
        /// Please note that it will be populated during initialization
        /// It will be ok to read them after the GameStarted event or after initialization
        /// </summary>
        public GameSettings Settings { get; private set; } // for easy transfer from PrepareContext to Initialize

        /// <summary>
        /// Gets the graphics device manager.
        /// </summary>
        /// <value>The graphics device manager.</value>
        public GraphicsDeviceManager GraphicsDeviceManager { get; internal set; }

        /// <summary>
        /// Gets the script system.
        /// </summary>
        /// <value>The script.</value>
        public ScriptSystem Script { get; }

        /// <summary>
        /// Gets the input manager.
        /// </summary>
        /// <value>The input.</value>
        public InputManager Input { get; internal set; }

        /// <summary>
        /// Gets the scene system.
        /// </summary>
        /// <value>The scene system.</value>
        public SceneSystem SceneSystem { get; }

        /// <summary>
        /// Gets the effect system.
        /// </summary>
        /// <value>The effect system.</value>
        public EffectSystem EffectSystem { get; private set; }

        /// <summary>
        /// Gets the streaming system.
        /// </summary>
        /// <value>The streaming system.</value>
        public StreamingManager Streaming { get; }

        /// <summary>
        /// Gets the audio system.
        /// </summary>
        /// <value>The audio.</value>
        public AudioSystem Audio { get; }

        /// <summary>
        /// Gets the sprite animation system.
        /// </summary>
        /// <value>The sprite animation system.</value>
        public SpriteAnimationSystem SpriteAnimation { get; }

        /// <summary>
        /// Gets the debug text system.
        /// </summary>
        public DebugTextSystem DebugTextSystem { get; }

        /// <summary>
        /// Gets the debug rendering system.
        /// </summary>
        public DebugRenderSystem DebugRenderSystem { get; }

        /// <summary>
        /// Gets the game profiler system.
        /// </summary>
        public GameProfilingSystem ProfilingSystem { get; }

        /// <summary>
        /// Gets the VR Device System.
        /// </summary>
        public VRDeviceSystem VRDeviceSystem { get; }

        /// <summary>
        /// Forces effects to be build in "Release" mode, regardless of GameSettings. Good for performance, even if your app is in debug mode.
        /// </summary>
        public bool ForceReleaseEffects { get; set; } = true;

        /// <summary>
        /// Forces effects to be recorded in "Release" mode, regardless of GameSettings. Good for collecting shaders for distribution.
        /// </summary>
        public bool ForceCollectEffects { get; set; } = false;

        /// <summary>
        /// Gets the font system.
        /// </summary>
        /// <value>The font system.</value>
        /// <exception cref="System.InvalidOperationException">The font system is not initialized yet</exception>
        public IFontFactory Font
        {
            get
            {
                if (gameFontSystem.FontSystem == null)
                    throw new InvalidOperationException("The font system is not initialized yet");

                return gameFontSystem.FontSystem;
            }
        }

        /// <summary>
        /// Gets or sets the console log mode. See remarks.
        /// </summary>
        /// <value>The console log mode.</value>
        /// <remarks>
        /// Defines how the console will be displayed when running the game. By default, on Windows, It will open only on debug
        /// if there are any messages logged.
        /// </remarks>
        public ConsoleLogMode ConsoleLogMode
        {
            get
            {
                var consoleLogListener = logListener as ConsoleLogListener;
                return consoleLogListener != null ? consoleLogListener.LogMode : default(ConsoleLogMode);
            }
            set
            {
                var consoleLogListener = logListener as ConsoleLogListener;
                if (consoleLogListener != null)
                {
                    consoleLogListener.LogMode = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the default console log level.
        /// </summary>
        /// <value>The console log level.</value>
        public LogMessageType ConsoleLogLevel
        {
            get
            {
                var consoleLogListener = logListener as ConsoleLogListener;
                return consoleLogListener != null ? consoleLogListener.LogLevel : default(LogMessageType);
            }
            set
            {
                var consoleLogListener = logListener as ConsoleLogListener;
                if (consoleLogListener != null)
                {
                    consoleLogListener.LogLevel = value;
                }
            }
        }

        /// <summary>
        /// Automatically initializes game settings like default scene, resolution, graphics profile.
        /// </summary>
        public bool AutoLoadDefaultSettings { get; set; }

        /// <summary>
        /// If AutoLoadDefaultSettings is true, these values will be set on game start. Set width & height to int.MaxValue to use highest native values.
        /// </summary>
        /// <returns>true if default settings changed</returns>
        public bool SetDefaultSettings(int width, int height, bool fullscreen, float? fov = null) {
            GetDefaultSettings(out int current_width, out int current_height, out bool current_fullscreen, out float outfov);
            if (width == current_width && height == current_height && current_fullscreen == fullscreen && (fov.HasValue == false || fov == outfov)) return false;
            try {
                System.IO.File.WriteAllText(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "/DefaultResolution.txt",
                                            width.ToString() + "\n" +
                                            height.ToString() + "\n" +
                                            (fullscreen ? "fullscreen" : "window") + "\n" +
                                            (fov ?? outfov).ToString());
                return true;
            } catch(Exception e) {
                return false;
            }
        }

        private bool settingsOverride = false, settingsOverrideFS;
        private int settingsOverrideW, settingsOverrideH;

        /// <summary>
        /// Temporarily uses different settings this run, without changing the saved default settings. Set width & height to int.MaxValue to use highest native values.
        /// </summary>
        public void OverrideDefaultSettings(int width, int height, bool fullscreen) {
            settingsOverride = true;
            settingsOverrideW = width;
            settingsOverrideH = height;
            settingsOverrideFS = fullscreen;
        }

        /// <summary>
        /// Gets default settings that will be used on game startup, if AutoLoadDefaultSettings is true. Caps resolution to native display resolution.
        /// </summary>
        public void GetDefaultSettings(out int width, out int height, out bool fullscreen, out float fov, Window useSDLWindow = null) {
            string defaultFile = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "/DefaultResolution.txt";
            // default settings are maximum native resolution
            bool gotCustomWH = false;
            width = int.MaxValue;
            height = int.MaxValue;
            fullscreen = true;
            fov = -1f;
            // wait, are we overriding settings?
            if (settingsOverride) {
                width = settingsOverrideW;
                height = settingsOverrideH;
                fullscreen = settingsOverrideFS;
                gotCustomWH = true;
            } else if (File.Exists(defaultFile)) {
                try {
                    string[] vals = File.ReadAllLines(defaultFile);
                    if (vals.Length >= 2)
                        gotCustomWH = int.TryParse(vals[0].Trim(), out width) && int.TryParse(vals[1].Trim(), out height);
                    if (vals.Length >= 3)
                        fullscreen = vals[2].Trim().ToLower().StartsWith("full");
                    if (vals.Length >= 4)
                        float.TryParse(vals[3].Trim(), out fov);
                } catch (Exception e) { }
            }
            try
            {
                // cap values to native resolution (try to use display window)
                int native_width, native_height, refresh_rate, index = 0;
                if (useSDLWindow is Window gwsdl) index = gwsdl.GetWindowDisplay();
                Graphics.SDL.Window.GetDisplayInformation(out native_width, out native_height, out refresh_rate, index);
                if (width >= native_width &&
                    height >= native_height)
                {
                    // force fullscreen if using native or higher,
                    // as crashes can happen on some hardware if using this big of a window
                    width = native_width;
                    height = native_height;
                    fullscreen = true;
                    fov = -1f;
                }
                else
                {
                    // smaller than native, lets make sure we are still within each bound though
                    if (width > native_width) width = native_width;
                    if (height > native_height) height = native_height;
                }
                gotCustomWH = true;
            }
            catch (Exception e) { }
            // make sure we got something valid
            if (gotCustomWH == false)
            {
                width = 1280;
                height = 720;
                fullscreen = false;
                fov = -1f;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Game"/> class.
        /// </summary>
        public Game()
        {
            // Register the logger backend before anything else
            logListener = GetLogListener();

            if (logListener != null)
                GlobalLogger.GlobalMessageLogged += logListener;

            // Create all core services, except Input which is created during `Initialize'.
            // Registration takes place in `Initialize'.
            Script = new ScriptSystem(Services);
            Services.AddService(Script);

            SceneSystem = new SceneSystem(Services);
            Services.AddService(SceneSystem);

            Streaming = new StreamingManager(Services);

            Audio = new AudioSystem(Services);
            Services.AddService(Audio);
            Services.AddService<IAudioEngineProvider>(Audio);

            gameFontSystem = new GameFontSystem(Services);
            Services.AddService(gameFontSystem.FontSystem);
            Services.AddService<IFontFactory>(gameFontSystem.FontSystem);

            SpriteAnimation = new SpriteAnimationSystem(Services);
            Services.AddService(SpriteAnimation);

            DebugTextSystem = new DebugTextSystem(Services);
            Services.AddService(DebugTextSystem);

            DebugRenderSystem = new DebugRenderSystem(Services);
            Services.AddService(DebugRenderSystem);

            ProfilingSystem = new GameProfilingSystem(Services);
            Services.AddService(ProfilingSystem);

            VRDeviceSystem = new VRDeviceSystem(Services);
            Services.AddService(VRDeviceSystem);

            // Creates the graphics device manager
            GraphicsDeviceManager = new GraphicsDeviceManager(this);
            Services.AddService<IGraphicsDeviceManager>(GraphicsDeviceManager);
            Services.AddService<IGraphicsDeviceService>(GraphicsDeviceManager);

            AutoLoadDefaultSettings = true;
        }

        /// <inheritdoc/>
        protected override void Destroy()
        {
            OnGameDestroyed(this);

            DestroyAssetDatabase();

            base.Destroy();

            if (logListener != null)
                GlobalLogger.GlobalMessageLogged -= logListener;
        }

        /// <inheritdoc/>
        protected override void PrepareContext()
        {
            base.PrepareContext();

            // Init assets
            if (Context.InitializeDatabase)
            {
                databaseFileProvider = InitializeAssetDatabase();
                ((DatabaseFileProviderService)Services.GetService<IDatabaseFileProviderService>()).FileProvider = databaseFileProvider;

                var renderingSettings = new RenderingSettings();
                if (Content.Exists(GameSettings.AssetUrl))
                {
                    Settings = Content.Load<GameSettings>(GameSettings.AssetUrl);

                    renderingSettings = Settings.Configurations.Get<RenderingSettings>();

                    // Set ShaderProfile even if AutoLoadDefaultSettings is false (because that is what shaders in effect logs are compiled against, even if actual instantiated profile is different)
                    if (renderingSettings.DefaultGraphicsProfile > 0)
                    {
                        var deviceManager = (GraphicsDeviceManager)graphicsDeviceManager;
                        if (!deviceManager.ShaderProfile.HasValue)
                            deviceManager.ShaderProfile = renderingSettings.DefaultGraphicsProfile;
                    }

                    Services.AddService<IGameSettingsService>(this);
                }

                // Load several default settings
                if (AutoLoadDefaultSettings)
                {
                    var deviceManager = (GraphicsDeviceManager)graphicsDeviceManager;
                    if (renderingSettings.DefaultGraphicsProfile > 0)
                    {
                        deviceManager.PreferredGraphicsProfile = new[] { renderingSettings.DefaultGraphicsProfile };
                    }

                    GetDefaultSettings(out renderingSettings.DefaultBackBufferWidth, out renderingSettings.DefaultBackBufferHeight, out bool fullScreen, out float fov, (Context as GameContextSDL)?.Control ?? null);
                    deviceManager.IsFullScreen = fullScreen;

                    if (renderingSettings.DefaultBackBufferWidth > 0) deviceManager.PreferredBackBufferWidth = renderingSettings.DefaultBackBufferWidth;
                    if (renderingSettings.DefaultBackBufferHeight > 0) deviceManager.PreferredBackBufferHeight = renderingSettings.DefaultBackBufferHeight;

                    deviceManager.PreferredColorSpace = renderingSettings.ColorSpace;
                    SceneSystem.OverrideFOV = fov;
                    SceneSystem.InitialSceneUrl = Settings?.DefaultSceneUrl;
                    SceneSystem.InitialGraphicsCompositorUrl = Settings?.DefaultGraphicsCompositorUrl;
                    SceneSystem.SplashScreenUrl = Settings?.SplashScreenUrl;
                    SceneSystem.SplashScreenColor = Settings?.SplashScreenColor ?? Color4.Black;
                    SceneSystem.DoubleViewSplashScreen = Settings?.DoubleViewSplashScreen ?? false;
                }

                // apply rendersettings
                Xenko.Graphics.Buffer.CaptureIndexBuffersOfSize = renderingSettings.CaptureIndexBufferOfSize;
                Xenko.Graphics.Buffer.CaptureVertexBuffersOfSize = renderingSettings.CaptureVertexBufferOfSize;
                ModelComponent.DefaultShadowCasters = renderingSettings.DefaultShadowCasters;

                // enable error file logging?
                if (renderingSettings.DisableErrorFileLog == false)
                {
                    ErrorFileLogger.EnableGlobalExceptionLogger();
                }
                else
                {
                    // should already be disabled, but we'll make sure
                    ErrorFileLogger.Disable();
                }
            }
        }

        public override void ConfirmRenderingSettings(bool gameCreation)
        {
            if (!AutoLoadDefaultSettings) return;

            var renderingSettings = Settings?.Configurations.Get<RenderingSettings>();
            if (renderingSettings == null) return;

            var deviceManager = (GraphicsDeviceManager)graphicsDeviceManager;

            if (gameCreation)
            {
                //execute the following steps only when the game is still at creation stage

                deviceManager.PreferredGraphicsProfile = Context.RequestedGraphicsProfile = new[] { renderingSettings.DefaultGraphicsProfile };

                //if our device height is actually smaller then requested we use the device one
                deviceManager.PreferredBackBufferHeight = Context.RequestedHeight = Math.Min(renderingSettings.DefaultBackBufferHeight, Window.ClientBounds.Height);
                //if our device width is actually smaller then requested we use the device one
                deviceManager.PreferredBackBufferWidth = Context.RequestedWidth = Math.Min(renderingSettings.DefaultBackBufferWidth, Window.ClientBounds.Width);
            }

            //these might get triggered even during game runtime, resize, orientation change

            if (renderingSettings.AdaptBackBufferToScreen)
            {
                var deviceAr = Window.ClientBounds.Width / (float)Window.ClientBounds.Height;

                if (renderingSettings.DefaultBackBufferHeight > renderingSettings.DefaultBackBufferWidth)
                {
                    deviceManager.PreferredBackBufferWidth = Context.RequestedWidth = (int)(deviceManager.PreferredBackBufferHeight * deviceAr);
                }
                else
                {
                    deviceManager.PreferredBackBufferHeight = Context.RequestedHeight = (int)(deviceManager.PreferredBackBufferWidth / deviceAr);
                }
            }
        }

        protected override void Initialize()
        {
            // ---------------------------------------------------------
            // Add common GameSystems - Adding order is important
            // (Unless overriden by gameSystem.UpdateOrder)
            // ---------------------------------------------------------

            // Add the input manager
            // Add it first so that it can obtained by the UI system
            Input = new InputManager(Services);
            Services.AddService(Input);
            GameSystems.Add(Input);

            // Initialize the systems
            base.Initialize();

            Content.Serializer.LowLevelSerializerSelector = ParameterContainerExtensions.DefaultSceneSerializerSelector;

            // Add the scheduler system
            // - Must be after Input, so that scripts are able to get latest input
            // - Must be before Entities/Camera/Audio/UI, so that scripts can apply
            // changes in the same frame they will be applied
            GameSystems.Add(Script);

            // Add the Font system
            GameSystems.Add(gameFontSystem);

            //Add the sprite animation System
            GameSystems.Add(SpriteAnimation);

            GameSystems.Add(DebugRenderSystem);
            GameSystems.Add(DebugTextSystem);
            GameSystems.Add(ProfilingSystem);

            EffectSystem = new EffectSystem(Services);
            Services.AddService(EffectSystem);

            // prepare effect compilation settings if we have a settings object
            if (Settings != null) {
                // align settings with what we may be forcing
                if (ForceReleaseEffects) Settings.CompilationMode = CompilationMode.Release;
                if (ForceCollectEffects) {
                    // we want to collect effects. make sure they are release versions
                    Settings.EffectCompilation = EffectCompilationMode.LocalOrRemote;
                    Settings.CompilationMode = CompilationMode.Release;
                    Settings.RecordUsedEffects = true;
                } else if (Settings.CompilationMode == CompilationMode.Testing || Settings.CompilationMode == CompilationMode.Debug) {
                    // don't collect debug shaders, we won't want to distribute them!
                    Settings.EffectCompilation = EffectCompilationMode.Local;
                    Settings.RecordUsedEffects = false;
                }
            }

            // If requested in game settings, compile effects remotely and/or notify new shader requests
            EffectSystem.Compiler = EffectCompilerFactory.CreateEffectCompiler(Content.FileProvider, EffectSystem, Settings?.PackageName,
                                                                               ForceCollectEffects ? EffectCompilationMode.LocalOrRemote : (Settings?.EffectCompilation ?? EffectCompilationMode.Local),
                                                                               ForceCollectEffects ? true : (Settings?.RecordUsedEffects ?? false));

            // Setup shader compiler settings from a compilation mode. 
            EffectSystem.SetCompilationMode(ForceReleaseEffects || ForceCollectEffects ? CompilationMode.Release : (Settings?.CompilationMode ?? CompilationMode.Release));

            GameSystems.Add(EffectSystem);

            if (Settings != null)
                Streaming.SetStreamingSettings(Settings.Configurations.Get<StreamingSettings>());
            GameSystems.Add(Streaming);
            GameSystems.Add(SceneSystem);

            // Add the Audio System
            GameSystems.Add(Audio);

            // Add the VR System
            GameSystems.Add(VRDeviceSystem);

            // TODO: data-driven?
            Content.Serializer.RegisterSerializer(new ImageSerializer());

            OnGameStarted(this);
        }

        internal static DatabaseFileProvider InitializeAssetDatabase()
        {
            using (Profiler.Begin(GameProfilingKeys.ObjectDatabaseInitialize))
            {
                // Create and mount database file system
                var objDatabase = ObjectDatabase.CreateDefaultDatabase();

                // Only set a mount path if not mounted already
                var mountPath = VirtualFileSystem.ResolveProviderUnsafe("/asset", true).Provider == null ? "/asset" : null;
                var result = new DatabaseFileProvider(objDatabase, mountPath);

                return result;
            }
        }

        private void DestroyAssetDatabase()
        {
            if (databaseFileProvider != null)
            {
                if (Services.GetService<IDatabaseFileProviderService>() is DatabaseFileProviderService dbfp)
                    dbfp.FileProvider = null;
                databaseFileProvider.Dispose();
                databaseFileProvider = null;
            }
        }

        /// <summary>
        /// Loads the content.
        /// </summary>
        protected virtual Task LoadContent()
        {
            return Task.FromResult(true);
        }

        internal override void LoadContentInternal()
        {
            base.LoadContentInternal();
            Script.AddTask(LoadContent);
        }
        protected virtual LogListener GetLogListener()
        {
            return new ConsoleLogListener();
        }

        private static void OnGameStarted(Game game)
        {
            GameStarted?.Invoke(game, null);
        }

        private static void OnGameDestroyed(Game game)
        {
            GameDestroyed?.Invoke(game, null);
        }
    }
}
