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

        private Matrix _viewMatrix;
        private Matrix _projectionMatrix;

        private Texture2D _sceneryTexture;
        private Effect _effect;

        private int [,] _floorPlan;
        private VertexBuffer _cityVertexBuffer;

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

            LoadFloorPlan ();

            base.Initialize ();
        }

        protected override void LoadContent ()
        {
            _spriteBatch = new SpriteBatch (GraphicsDevice);

            _sceneryTexture = Content.Load<Texture2D> ("texturemap");
            byte [] bytecode = File.ReadAllBytes (@"Content/CompiledEffects/effects.mgfx");
            _effect = new Effect (_device, bytecode);

            SetupCamera ();
            SetupVertices ();
        }

        protected override void Update (GameTime gameTime)
        {
            if (GamePad.GetState (PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState ().IsKeyDown (Keys.Escape))
                Exit ();

            // TODO: Add your update logic here

            base.Update (gameTime);
        }

        protected override void Draw (GameTime gameTime)
        {
            GraphicsDevice.Clear (Color.CornflowerBlue);

            DrawCity ();

            base.Draw (gameTime);
        }

        private void SetupCamera ()
        {
            _viewMatrix = Matrix.CreateLookAt (new Vector3 (3, 5, 2), new Vector3 (2, 0, -1), new Vector3 (0, 1, 0));
            _projectionMatrix = Matrix.CreatePerspectiveFieldOfView (MathHelper.PiOver4, _device.Viewport.AspectRatio, 0.2f, 500.0f);
        }

        private void SetupVertices ()
        {
            int cityWidth = _floorPlan.GetLength (0);
            int cityLength = _floorPlan.GetLength (1);

            int imagesInTexture = 11;
            int desiredImage = 0;

            List<VertexPositionNormalTexture> verticesList = new List<VertexPositionNormalTexture> ();
            for (int x = 0; x < cityWidth; x++)
            {
                for (int z = 0; z < cityLength; z++)
                {
                    if (_floorPlan [x, z] == 0)
                    {
                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x, 0, -z), new Vector3 (0, 1, 0), new Vector2 ((desiredImage + 0f) / imagesInTexture, 1)));
                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x, 0, -z - 1), new Vector3 (0, 1, 0), new Vector2 ((desiredImage + 0f) / imagesInTexture, 0)));
                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x + 1, 0, -z), new Vector3 (0, 1, 0), new Vector2 ((desiredImage + 1.0f) / imagesInTexture, 1)));

                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x, 0, -z - 1), new Vector3 (0, 1, 0), new Vector2 ((desiredImage + 0f) / imagesInTexture, 0)));
                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x + 1, 0, -z - 1), new Vector3 (0, 1, 0), new Vector2 ((desiredImage + 1.0f) / imagesInTexture, 0)));
                        verticesList.Add (new VertexPositionNormalTexture (new Vector3 (x + 1, 0, -z), new Vector3 (0, 1, 0), new Vector2 ((desiredImage + 1.0f) / imagesInTexture, 1)));
                    }
                }
            }

            _cityVertexBuffer = new VertexBuffer (_device, VertexPositionNormalTexture.VertexDeclaration, verticesList.Count, BufferUsage.WriteOnly);
            _cityVertexBuffer.SetData<VertexPositionNormalTexture> (verticesList.ToArray ());
        }

        private void LoadFloorPlan ()
        {
            _floorPlan = new int [,]
            {
                {0, 0, 0},
                {0, 1, 0},
                {0, 0, 0}
            };
        }

        private void DrawCity ()
        {
            _effect.CurrentTechnique = _effect.Techniques ["Textured"];
            _effect.Parameters ["xWorld"].SetValue (Matrix.Identity);
            _effect.Parameters ["xView"].SetValue (_viewMatrix);
            _effect.Parameters ["xProjection"].SetValue (_projectionMatrix);
            _effect.Parameters ["xTexture"].SetValue (_sceneryTexture);

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
    }
}