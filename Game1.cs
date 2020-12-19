using System;
using System.Collections.Generic;
using System.IO;



using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace _3D_FlightSim
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private GraphicsDevice _device;
        private Random _random;

        private Matrix _viewMatrix;
        private Matrix _projectionMatrix;

        private Texture2D _sceneryTexture;
        private Effect _effect;
        private Model _xwingModel;

        private Vector3 _lightDirection = new Vector3 (3, -2, 5);
        private float _ambientLight = 0.3f;

        private int[,] _floorPlan;
        private VertexBuffer _cityVertexBuffer;

        private int[] _buildingHeights = new int[] { 0, 2, 2, 6, 5, 4 };

        private bool _isDebug = false;
        private SpriteFont _debugFont;

        private KeyboardState _lastKeyboardState;

        public Game1 ()
        {
            _graphics = new GraphicsDeviceManager (this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize ()
        {
            _device = _graphics.GraphicsDevice;
            _graphics.PreferredBackBufferWidth = 500;
            _graphics.PreferredBackBufferHeight = 500;
            _graphics.IsFullScreen = false;
            _graphics.ApplyChanges ();
            Window.Title = "Riemer's 3D Flight Simulator";

            _random = new Random ();
            _lightDirection.Normalize ();

            _lastKeyboardState = Keyboard.GetState ();

            LoadFloorPlan ();

            base.Initialize ();
        }

        protected override void LoadContent ()
        {
            _spriteBatch = new SpriteBatch (GraphicsDevice);

            _sceneryTexture = Content.Load<Texture2D> (@"Textures\texturemap");
            byte[] bytecode = File.ReadAllBytes (@"Content/CompiledEffects/effects.mgfx");
            _effect = new Effect (_device, bytecode);

            _xwingModel = LoadModel (@"Models\xwing");

            _debugFont = Content.Load<SpriteFont> (@"Fonts\Arial\myFont");

            SetupCamera ();
            SetupVertices ();
        }

        protected override void Update (GameTime gameTime)
        {
            if (GamePad.GetState (PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState ().IsKeyDown (Keys.Escape))
                Exit ();

            // TODO: Add your update logic here
            ProcessKeyboard ();

            base.Update (gameTime);
        }

        private void ProcessKeyboard ()
        {
            KeyboardState keyState = Keyboard.GetState ();

            if (keyState.IsKeyDown (Keys.Q) && _lastKeyboardState.IsKeyUp (Keys.Q))
            {
                _isDebug = !_isDebug;
            }

            if (_isDebug)
            {
                if (keyState.IsKeyDown (Keys.U) && _lastKeyboardState.IsKeyUp (Keys.U))
                {
                    _ambientLight += 0.05f;
                }
                if (keyState.IsKeyDown (Keys.J) && _lastKeyboardState.IsKeyUp (Keys.J))
                {
                    _ambientLight -= 0.05f;
                }
            }

            _lastKeyboardState = keyState;
        }

        protected override void Draw (GameTime gameTime)
        {
            GraphicsDevice.Clear (ClearOptions.Target | ClearOptions.DepthBuffer, Color.DarkSlateBlue, 1.0f, 0);

            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            DrawCity ();
            DrawModel (_xwingModel);

            if (_isDebug)
            {
                _spriteBatch.Begin ();
                _spriteBatch.DrawString (
                    _debugFont,
                    "DEBUG",
                    new Vector2 (10, 10),
                    Color.Red
                );
                _spriteBatch.End ();
            }

            base.Draw (gameTime);
        }

        private void SetupCamera ()
        {
            _viewMatrix = Matrix.CreateLookAt (new Vector3 (20, 13, -5), new Vector3 (8, 0, -7), new Vector3 (0, 1, 0));
            _projectionMatrix = Matrix.CreatePerspectiveFieldOfView (MathHelper.PiOver4, _device.Viewport.AspectRatio, 0.2f, 500.0f);
        }

        private void SetupVertices ()
        {
            int cityWidth = _floorPlan.GetLength (0);
            int cityLength = _floorPlan.GetLength (1);

            int differentBuildings = _buildingHeights.Length - 1;
            float imagesInTexture = 1 + differentBuildings * 2;

            List<VertexPositionNormalTexture> verticesList = new List<VertexPositionNormalTexture> ();
            for (int x = 0; x < cityWidth; x++)
            {
                for (int z = 0; z < cityLength; z++)
                {
                    int currentBuilding = _floorPlan[x, z];
                    int desiredImage = currentBuilding * 2;
                    int buildingHeight = _buildingHeights[currentBuilding];

                    // floor or ceiling
                    verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x, buildingHeight, -z), new Vector3 (0, 1, 0), new Vector2 ((desiredImage + 0f) / imagesInTexture, 1)));
                    verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x, buildingHeight, -z - 1), new Vector3 (0, 1, 0), new Vector2 ((desiredImage + 0f) / imagesInTexture, 0)));
                    verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x + 1, buildingHeight, -z), new Vector3 (0, 1, 0), new Vector2 ((desiredImage + 1.0f) / imagesInTexture, 1)));

                    verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x, buildingHeight, -z - 1), new Vector3 (0, 1, 0), new Vector2 ((desiredImage + 0f) / imagesInTexture, 0)));
                    verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x + 1, buildingHeight, -z - 1), new Vector3 (0, 1, 0), new Vector2 ((desiredImage + 1.0f) / imagesInTexture, 0)));
                    verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x + 1, buildingHeight, -z), new Vector3 (0, 1, 0), new Vector2 ((desiredImage + 1.0f) / imagesInTexture, 1)));

                    if (currentBuilding != 0)
                    {
                        // front wall
                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x + 1, 0, -z - 1), new Vector3 (0, 0, -1), new Vector2 (desiredImage / imagesInTexture, 1)));
                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x, buildingHeight, -z - 1), new Vector3 (0, 0, -1), new Vector2 ((desiredImage - 1) / imagesInTexture, 0)));
                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x, 0, -z - 1), new Vector3 (0, 0, -1), new Vector2 ((desiredImage - 1) / imagesInTexture, 1)));

                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x, buildingHeight, -z - 1), new Vector3 (0, 0, -1), new Vector2 ((desiredImage - 1) / imagesInTexture, 0)));
                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x + 1, 0, -z - 1), new Vector3 (0, 0, -1), new Vector2 (desiredImage / imagesInTexture, 1)));
                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x + 1, buildingHeight, -z - 1), new Vector3 (0, 0, -1), new Vector2 (desiredImage / imagesInTexture, 0)));

                        //back wall
                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x + 1, 0, -z), new Vector3 (0, 0, 1), new Vector2 (desiredImage / imagesInTexture, 1)));
                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x, 0, -z), new Vector3 (0, 0, 1), new Vector2 ((desiredImage - 1) / imagesInTexture, 1)));
                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x, buildingHeight, -z), new Vector3 (0, 0, 1), new Vector2 ((desiredImage - 1) / imagesInTexture, 0)));

                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x, buildingHeight, -z), new Vector3 (0, 0, 1), new Vector2 ((desiredImage - 1) / imagesInTexture, 0)));
                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x + 1, buildingHeight, -z), new Vector3 (0, 0, 1), new Vector2 (desiredImage / imagesInTexture, 0)));
                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x + 1, 0, -z), new Vector3 (0, 0, 1), new Vector2 (desiredImage / imagesInTexture, 1)));

                        //left wall
                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x, 0, -z), new Vector3 (-1, 0, 0), new Vector2 (desiredImage / imagesInTexture, 1)));
                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x, 0, -z - 1), new Vector3 (-1, 0, 0), new Vector2 ((desiredImage - 1) / imagesInTexture, 1)));
                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x, buildingHeight, -z - 1), new Vector3 (-1, 0, 0), new Vector2 ((desiredImage - 1) / imagesInTexture, 0)));

                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x, buildingHeight, -z - 1), new Vector3 (-1, 0, 0), new Vector2 ((desiredImage - 1) / imagesInTexture, 0)));
                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x, buildingHeight, -z), new Vector3 (-1, 0, 0), new Vector2 (desiredImage / imagesInTexture, 0)));
                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x, 0, -z), new Vector3 (-1, 0, 0), new Vector2 (desiredImage / imagesInTexture, 1)));

                        //right wall
                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x + 1, 0, -z), new Vector3 (1, 0, 0), new Vector2 (desiredImage / imagesInTexture, 1)));
                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x + 1, buildingHeight, -z - 1), new Vector3 (1, 0, 0), new Vector2 ((desiredImage - 1) / imagesInTexture, 0)));
                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x + 1, 0, -z - 1), new Vector3 (1, 0, 0), new Vector2 ((desiredImage - 1) / imagesInTexture, 1)));

                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x + 1, buildingHeight, -z - 1), new Vector3 (1, 0, 0), new Vector2 ((desiredImage - 1) / imagesInTexture, 0)));
                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x + 1, 0, -z), new Vector3 (1, 0, 0), new Vector2 (desiredImage / imagesInTexture, 1)));
                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x + 1, buildingHeight, -z), new Vector3 (1, 0, 0), new Vector2 (desiredImage / imagesInTexture, 0)));
                    }

                }
            }

            _cityVertexBuffer = new VertexBuffer (_device, VertexPositionNormalTexture.VertexDeclaration, verticesList.Count, BufferUsage.WriteOnly);
            _cityVertexBuffer.SetData<VertexPositionNormalTexture> (verticesList.ToArray ());
        }

        private void LoadFloorPlan ()
        {
            _floorPlan = new int[,]
            {
                {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
                {1,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
                {1,0,0,1,1,0,0,0,1,1,0,0,1,0,1},
                {1,0,0,1,1,0,0,0,1,0,0,0,1,0,1},
                {1,0,0,0,1,1,0,1,1,0,0,0,0,0,1},
                {1,0,0,0,0,0,0,0,0,0,0,1,0,0,1},
                {1,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
                {1,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
                {1,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
                {1,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
                {1,0,1,1,0,0,0,1,0,0,0,0,0,0,1},
                {1,0,1,0,0,0,0,0,0,0,0,0,0,0,1},
                {1,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
                {1,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
                {1,0,0,0,0,1,0,0,0,0,0,0,0,0,1},
                {1,0,0,0,0,1,0,0,0,1,0,0,0,0,1},
                {1,0,1,0,0,0,0,0,0,1,0,0,0,0,1},
                {1,0,1,1,0,0,0,0,1,1,0,0,0,1,1},
                {1,0,0,0,0,0,0,0,1,1,0,0,0,1,1},
                {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
            };

            int differentBuildings = _buildingHeights.Length - 1;

            for (int x = 0; x < _floorPlan.GetLength (0); x++)
            {
                for (int y = 0; y < _floorPlan.GetLength (1); y++)
                {
                    if (_floorPlan[x, y] == 1)
                    {
                        _floorPlan[x, y] = _random.Next (differentBuildings) + 1;
                    }
                }
            }
        }

        private void DrawCity ()
        {
            _effect.CurrentTechnique = _effect.Techniques["Textured"];
            _effect.Parameters["xWorld"].SetValue (Matrix.Identity);
            _effect.Parameters["xView"].SetValue (_viewMatrix);
            _effect.Parameters["xProjection"].SetValue (_projectionMatrix);
            _effect.Parameters["xTexture"].SetValue (_sceneryTexture);
            _effect.Parameters["xEnableLighting"].SetValue (true);
            _effect.Parameters["xLightDirection"].SetValue (_lightDirection);
            _effect.Parameters["xAmbient"].SetValue (_ambientLight);

            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply ();
                _device.SetVertexBuffer (_cityVertexBuffer);
                _device.DrawPrimitives (
                    PrimitiveType.TriangleList,
                    0,
                    _cityVertexBuffer.VertexCount / 3
                );
            }
        }

        private void DrawModel (Model model)
        {
            float scale = 0.0005f;
            Matrix worldMatrix =
                Matrix.CreateScale (scale, scale, scale) *
                Matrix.CreateRotationY (MathHelper.Pi) *
                Matrix.CreateTranslation (new Vector3 (19, 12, -5));

            Matrix[] transforms = new Matrix[model.Bones.Count];
            model.CopyAbsoluteBoneTransformsTo (transforms);

            foreach (var mesh in model.Meshes)
            {
                foreach (var effect in mesh.Effects)
                {
                    effect.CurrentTechnique = effect.Techniques["Colored"];
                    effect.Parameters["xWorld"].SetValue (transforms[mesh.ParentBone.Index] * worldMatrix);
                    effect.Parameters["xView"].SetValue (_viewMatrix);
                    effect.Parameters["xProjection"].SetValue (_projectionMatrix);
                    _effect.Parameters["xEnableLighting"].SetValue (true);
                    _effect.Parameters["xLightDirection"].SetValue (_lightDirection);
                    _effect.Parameters["xAmbient"].SetValue (_ambientLight);
                }
                mesh.Draw ();
            }

        }

        private Model LoadModel (string assetName)
        {
            Model newModel = Content.Load<Model> (assetName);
            foreach (var mesh in newModel.Meshes)
            {
                foreach (var part in mesh.MeshParts)
                {
                    part.Effect = _effect.Clone ();
                }
            }
            return newModel;
        }
    }
}