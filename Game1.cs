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
        private Vector3 _xwingPosition = new Vector3 (8, 1, -3);
        private Quaternion _xwingRotation = Quaternion.Identity;

        private Vector3 _lightDirection = new Vector3 (3, -2, 5);
        private float _ambientLight = 0.3f;

        private int[,] _floorPlan;
        private VertexBuffer _cityVertexBuffer;

        private int[] _buildingHeights = new int[] { 0, 2, 2, 6, 5, 4 };

        private bool _isDebug = false;
        private SpriteFont _debugFont;

        private KeyboardState _lastKeyboardState;
        private float _gameSpeed = 1.0f;

        // collision detection support
        private BoundingBox[] _buildingboundingBoxes;
        private BoundingBox _cityBounds;
        private List<BoundingSphere> _targetList = new List<BoundingSphere> ();

        // targets 
        private Model _targetModel;
        private const int _maxTargets = 50;

        // bullets
        private List<Bullet> _bulletList = new List<Bullet> ();
        private double _lastBulletTime = 0;
        private double _bulletCooldownTime = 100;
        private Texture2D _bulletTexture;

        private Vector3 _cameraPosition;
        private Vector3 _cameraUpDirection;


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
            _bulletTexture = Content.Load<Texture2D> (@"Textures\bullet");
            byte[] bytecode = File.ReadAllBytes (@"Content/CompiledEffects/effects.mgfx");
            _effect = new Effect (_device, bytecode);

            _xwingModel = LoadModel (@"Models\xwing");
            _targetModel = LoadModel (@"Models\target");

            _debugFont = Content.Load<SpriteFont> (@"Fonts\Arial\myFont");

            SetupCamera ();
            SetupVertices ();
            SetupBoundingBoxes ();
            AddTargets ();
        }

        protected override void Update (GameTime gameTime)
        {
            if (GamePad.GetState (PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState ().IsKeyDown (Keys.Escape))
                Exit ();

            // TODO: Add your update logic here
            UpdateCamera ();
            ProcessKeyboard (gameTime);

            var speed = gameTime.ElapsedGameTime.Milliseconds / 500.0f * _gameSpeed;
            MoveForward (ref _xwingPosition, _xwingRotation, speed);

            BoundingSphere xwingSphere = new BoundingSphere (_xwingPosition, 0.04f);
            if (CheckCollision (xwingSphere) != CollisionType.None)
            {
                _xwingPosition = new Vector3 (8, 1, -3);
                _xwingRotation = Quaternion.Identity;
                _gameSpeed /= 1.1f;
            }

            UpdateBulletPositions (speed);

            base.Update (gameTime);
        }

        private void ProcessKeyboard (GameTime gameTime)
        {
            KeyboardState keyState = Keyboard.GetState ();

            // moving

            float leftRightRotation = 0;
            float turningSpeed = (float)gameTime.ElapsedGameTime.TotalMilliseconds / 1000.0f;
            turningSpeed *= 1.6f * _gameSpeed;

            if (keyState.IsKeyDown (Keys.Right)) leftRightRotation += turningSpeed;
            if (keyState.IsKeyDown (Keys.Left)) leftRightRotation -= turningSpeed;

            float upDownRotation = 0;
            if (keyState.IsKeyDown (Keys.Down)) upDownRotation += turningSpeed;
            if (keyState.IsKeyDown (Keys.Up)) upDownRotation -= turningSpeed;

            Quaternion additionalRotation =
                Quaternion.CreateFromAxisAngle (new Vector3 (0, 0, -1), leftRightRotation) *
                Quaternion.CreateFromAxisAngle (new Vector3 (1, 0, 0), upDownRotation);
            _xwingRotation *= additionalRotation;

            // shooting
            if (keyState.IsKeyDown (Keys.Space))
            {
                double currentTime = gameTime.TotalGameTime.TotalMilliseconds;
                if (currentTime - _lastBulletTime > _bulletCooldownTime)
                {
                    _bulletList.Add (
                        new Bullet ()
                        {
                            Position = _xwingPosition,
                            Rotation = _xwingRotation
                        }
                    );

                    _lastBulletTime = currentTime;
                }
            }

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

        private void MoveForward (ref Vector3 position, Quaternion rotationQuat, float speed)
        {
            Vector3 addVector = Vector3.Transform (new Vector3 (0, 0, -1), rotationQuat);
            position += addVector * speed;
        }

        protected override void Draw (GameTime gameTime)
        {
            GraphicsDevice.Clear (ClearOptions.Target | ClearOptions.DepthBuffer, Color.DarkSlateBlue, 1.0f, 0);

            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            DrawCity ();
            DrawModel ();
            DrawTargets ();
            DrawBullets ();

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

        private void UpdateCamera ()
        {
            // offset from xwing model
            Vector3 cameraPosition = new Vector3 (0, 0.1f, 0.6f);

            // rotate the camera to align with the rotation of the xwing 
            cameraPosition = Vector3.Transform (cameraPosition, Matrix.CreateFromQuaternion (_xwingRotation));

            // move the camera so that it ligns up behind the xwing
            cameraPosition += _xwingPosition;

            // based on the rotation, figure out which direction is 'up' for the camera
            Vector3 cameraUp = Vector3.Up;
            cameraUp = Vector3.Transform (cameraUp, Matrix.CreateFromQuaternion (_xwingRotation));

            // update the view and projection matrices
            _viewMatrix = Matrix.CreateLookAt (cameraPosition, _xwingPosition, cameraUp);
            _projectionMatrix = Matrix.CreatePerspectiveFieldOfView (MathHelper.PiOver4, _device.Viewport.AspectRatio, 0.2f, 500.0f);
            _cameraPosition = cameraPosition;
            _cameraUpDirection = cameraUp;
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

        private void DrawModel ()
        {
            float scale = 0.0005f;
            Matrix worldMatrix =
                Matrix.CreateScale (scale, scale, scale) *
                Matrix.CreateRotationY (MathHelper.Pi) *
                Matrix.CreateFromQuaternion (_xwingRotation) *
                Matrix.CreateTranslation (_xwingPosition);

            Matrix[] transforms = new Matrix[_xwingModel.Bones.Count];
            _xwingModel.CopyAbsoluteBoneTransformsTo (transforms);

            foreach (var mesh in _xwingModel.Meshes)
            {
                foreach (var effect in mesh.Effects)
                {
                    effect.CurrentTechnique = effect.Techniques["Colored"];
                    effect.Parameters["xWorld"].SetValue (transforms[mesh.ParentBone.Index] * worldMatrix);
                    effect.Parameters["xView"].SetValue (_viewMatrix);
                    effect.Parameters["xProjection"].SetValue (_projectionMatrix);
                    effect.Parameters["xEnableLighting"].SetValue (true);
                    effect.Parameters["xLightDirection"].SetValue (_lightDirection);
                    effect.Parameters["xAmbient"].SetValue (_ambientLight);
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

        // targets
        private void AddTargets ()
        {
            int cityWidth = _floorPlan.GetLength (0);
            int cityLength = _floorPlan.GetLength (1);

            while (_targetList.Count < _maxTargets)
            {
                int x = _random.Next (cityWidth);
                int z = -_random.Next (cityLength);
                float y = (float)_random.Next (2000) / 1000f + 1;
                float radius = (float)_random.Next (1000) / 1000f * 0.2f + 0.01f;

                BoundingSphere newTarget = new BoundingSphere (new Vector3 (x, y, z), radius);

                if (CheckCollision (newTarget) == CollisionType.None)
                {
                    _targetList.Add (newTarget);
                }
            }
        }

        private void DrawTargets ()
        {
            for (int i = 0; i < _targetList.Count; i++)
            {
                Matrix worldMatrix =
                    Matrix.CreateScale (_targetList[i].Radius) *
                    Matrix.CreateTranslation (_targetList[i].Center);

                Matrix[] targetTransforms = new Matrix[_targetModel.Bones.Count];
                _targetModel.CopyAbsoluteBoneTransformsTo (targetTransforms);

                foreach (var mesh in _targetModel.Meshes)
                {
                    foreach (var effect in mesh.Effects)
                    {
                        effect.CurrentTechnique = effect.Techniques["Colored"];
                        effect.Parameters["xWorld"].SetValue (targetTransforms[mesh.ParentBone.Index] * worldMatrix);
                        effect.Parameters["xView"].SetValue (_viewMatrix);
                        effect.Parameters["xProjection"].SetValue (_projectionMatrix);
                        effect.Parameters["xEnableLighting"].SetValue (true);
                        effect.Parameters["xLightDirection"].SetValue (_lightDirection);
                        effect.Parameters["xAmbient"].SetValue (_ambientLight);
                    }
                    mesh.Draw ();
                }
            }
        }

        // collision detection support
        private void SetupBoundingBoxes ()
        {
            int cityWidth = _floorPlan.GetLength (0);
            int cityLength = _floorPlan.GetLength (1);

            List<BoundingBox> bbList = new List<BoundingBox> ();
            for (int x = 0; x < cityWidth; x++)
            {
                for (int z = 0; z < cityLength; z++)
                {
                    int buildingType = _floorPlan[x, z];
                    if (buildingType != 0)
                    {
                        int buildingHeight = _buildingHeights[buildingType];
                        Vector3[] buildingPoints = new Vector3[2];
                        buildingPoints[0] = new Vector3 (x, 0, -z);
                        buildingPoints[1] = new Vector3 (x + 1, buildingHeight, -z - 1);

                        BoundingBox buildingBox = BoundingBox.CreateFromPoints (buildingPoints);
                        bbList.Add (buildingBox);
                    }
                }
            }

            _buildingboundingBoxes = bbList.ToArray ();

            Vector3[] boundaryPoints = new Vector3[2];
            boundaryPoints[0] = Vector3.Zero;
            boundaryPoints[1] = new Vector3 (cityWidth, 20, -cityLength);
            _cityBounds = BoundingBox.CreateFromPoints (boundaryPoints);
        }

        private CollisionType CheckCollision (BoundingSphere sphere)
        {
            for (int i = 0; i < _buildingboundingBoxes.Length; i++)
            {
                if (_buildingboundingBoxes[i].Contains (sphere) != ContainmentType.Disjoint)
                {
                    return CollisionType.Building;
                }
            }

            if (_cityBounds.Contains (sphere) != ContainmentType.Contains)
            {
                return CollisionType.Boundary;
            }

            for (int i = _targetList.Count - 1; i >= 0; i--)
            {
                if (_targetList[i].Contains (sphere) != ContainmentType.Disjoint)
                {
                    _targetList.RemoveAt (i);
                    AddTargets ();

                    return CollisionType.Target;
                }
            }

            return CollisionType.None;
        }

        private void UpdateBulletPositions (float moveSpeed)
        {
            for (int i = 0; i < _bulletList.Count; i++)
            {
                Bullet currentBullet = _bulletList[i];
                MoveForward (ref currentBullet.Position, currentBullet.Rotation, moveSpeed * 2.0f);
                _bulletList[i] = currentBullet;
            }
        }

        private void DrawBullets ()
        {
            if (_bulletList.Count > 0)
            {
                VertexPositionTexture[] bulletVertices = new VertexPositionTexture[_bulletList.Count * 6];
                int i = 0;
                foreach (var bullet in _bulletList)
                {
                    Vector3 center = bullet.Position;

                    // bottom right->upper left->upper right
                    bulletVertices[i++] = new VertexPositionTexture (center, new Vector2 (1, 1));
                    bulletVertices[i++] = new VertexPositionTexture (center, new Vector2 (0, 0));
                    bulletVertices[i++] = new VertexPositionTexture (center, new Vector2 (1, 0));

                    // bottom right->lower left->upper left
                    bulletVertices[i++] = new VertexPositionTexture (center, new Vector2 (1, 1));
                    bulletVertices[i++] = new VertexPositionTexture (center, new Vector2 (0, 1));
                    bulletVertices[i++] = new VertexPositionTexture (center, new Vector2 (0, 0));
                }


                _effect.CurrentTechnique = _effect.Techniques["PointSprites"];
                _effect.Parameters["xWorld"].SetValue (Matrix.Identity);
                _effect.Parameters["xProjection"].SetValue (_projectionMatrix);
                _effect.Parameters["xView"].SetValue (_viewMatrix);
                _effect.Parameters["xCamPos"].SetValue (_cameraPosition);
                _effect.Parameters["xCamUp"].SetValue (_cameraUpDirection);
                _effect.Parameters["xTexture"].SetValue (_bulletTexture);
                _effect.Parameters["xPointSpriteSize"].SetValue (0.1f);

                foreach (var pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply ();
                    _device.DrawUserPrimitives (PrimitiveType.TriangleList, bulletVertices, 0, _bulletList.Count * 2);
                }
            }
        }
    }
}