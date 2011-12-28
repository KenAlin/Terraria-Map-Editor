﻿using System;
using System.Windows;
using System.Windows.Controls;
using BCCL.Geometry.Primitives;
using BCCL.UI.Xaml.XnaContentHost;
using BCCL.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TEditXNA.Terraria;
using TEditXna.Editor;
using TEditXna.Editor.Tools;
using TEditXna.ViewModel;
using System.Windows.Media.Imaging;
using Point = System.Windows.Point;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace TEditXna.View
{
    /// <summary>
    /// Interaction logic for WorldRenderXna.xaml
    /// </summary>
    public partial class WorldRenderXna : UserControl
    {
        private Color _backgroundColor = Color.FromNonPremultiplied(32, 32, 32, 255);
        private readonly GameTimer _gameTimer;
        private readonly WorldViewModel _wvm;
        private bool _isMiddleMouseDown;
        private Vector2 _middleClickPoint;
        private Vector2 _mousePosition;
        private Vector2 _scrollPosition = new Vector2(0, 0);
        private SimpleProvider _serviceProvider;
        private SpriteBatch _spriteBatch;
        private Textures _textureDictionary;
        private Texture2D[] _tileMap;
        private Texture2D _preview;
        private float _zoom = 1;

        public WorldRenderXna()
        {
            // to stop visual studio design time crash :(
            if (!Debugging.IsInDesignMode)
            {
                InitializeComponent();
                _gameTimer = new GameTimer();
            }

            _wvm = ViewModelLocator.WorldViewModel;
            _wvm.PreviewChanged += PreviewChanged;
        }

        private void PreviewChanged(object sender, EventArgs e)
        {
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (xnaViewport.GraphicsService.GraphicsDevice != null)
                _preview = _wvm.ActiveTool.PreviewTool().ToTexture2D(xnaViewport.GraphicsService.GraphicsDevice);
        }

        private static Vector2 PointToVector2(Point point)
        {
            return new Vector2((float)point.X, (float)point.Y);
        }

        private void InitializeGraphicsComponents(GraphicsDeviceEventArgs e)
        {
            // Load services, textures and initialize spritebatch
            _serviceProvider = new SimpleProvider(xnaViewport.GraphicsService);
            _spriteBatch = new SpriteBatch(e.GraphicsDevice);
            _textureDictionary = new Textures(_serviceProvider);
        }

        public void CenterOnTile(int x, int y)
        {
            _scrollPosition = new Vector2(
                -x + (float)(xnaViewport.ActualWidth / _zoom / 2),
                -y + (float)(xnaViewport.ActualHeight / _zoom / 2));
        }

        #region Load Content

        private void xnaViewport_LoadContent(object sender, GraphicsDeviceEventArgs e)
        {
            // Abort rendering if in design mode or if gameTimer is already running
            if (Debugging.IsInDesignMode || _gameTimer.IsRunning)
                return;

            InitializeGraphicsComponents(e);
            LoadTerrariaTextures();

            // Start the Game Timer
            _gameTimer.Start();
        }

        private void LoadTerrariaTextures()
        {
            // If the texture dictionary is valid (Found terraria and loaded content) load texture data
            if (_textureDictionary.Valid)
            {
            }
        }

        #endregion

        #region Update

        private void xnaViewport_RenderXna(object sender, GraphicsDeviceEventArgs e)
        {
            // Clear the graphics device and texture buffer
            e.GraphicsDevice.Clear(_backgroundColor);

            // Abort rendering if in design mode or if gameTimer is not running
            if (Debugging.IsInDesignMode || !_gameTimer.IsRunning || _wvm.CurrentWorld == null)
                return;

            Update(e);
            Render(e);
        }

        private void Update(GraphicsDeviceEventArgs e)
        {
            // Update
            _gameTimer.Update();


            ScrollWorld();
        }

        private void ScrollWorld()
        {
            if (_isMiddleMouseDown)
            {
                Vector2 stretchDistance = (_mousePosition - _middleClickPoint);
                Vector2 clampedScroll = _scrollPosition + stretchDistance / _zoom;
                _scrollPosition = clampedScroll;
                _middleClickPoint = _mousePosition;
            }

            int xNormalRange = -_wvm.CurrentWorld.TilesWide + (int)(xnaViewport.ActualWidth / _zoom);
            int yNormalRange = -_wvm.CurrentWorld.TilesHigh + (int)(xnaViewport.ActualHeight / _zoom);

            if (_wvm.CurrentWorld.TilesWide > (int)(xnaViewport.ActualWidth / _zoom))
                _scrollPosition.X = MathHelper.Clamp(_scrollPosition.X, xNormalRange, 0);
            else
                _scrollPosition.X = MathHelper.Clamp(_scrollPosition.X, (_wvm.CurrentWorld.TilesWide / 2 - (int)(xnaViewport.ActualWidth / _zoom) / 2), 0);

            if (_wvm.CurrentWorld.TilesHigh > (int)(xnaViewport.ActualHeight / _zoom))
                _scrollPosition.Y = MathHelper.Clamp(_scrollPosition.Y, yNormalRange, 0);
            else
                _scrollPosition.Y = MathHelper.Clamp(_scrollPosition.Y, (_wvm.CurrentWorld.TilesHigh / 2 - (int)(xnaViewport.ActualHeight / _zoom) / 2), 0);
        }

        #endregion

        #region Render

        private void Render(GraphicsDeviceEventArgs e)
        {
            // Clear the graphics device and texture buffer
            //e.GraphicsDevice.Clear(Color.Black);
            e.GraphicsDevice.Textures[0] = null;

            GenPixelTiles(e);

            // Start SpriteBatch
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone);


            // Draw Pixel Map tiles
            DrawPixelTiles();

            // Draw sprite overlays
            DrawSprites();

            DrawToolPreview();

            // End SpriteBatch
            _spriteBatch.End();
        }



        private void GenPixelTiles(GraphicsDeviceEventArgs e)
        {
            if (_wvm != null)
            {
                if (_wvm.PixelMap != null)
                {
                    if (_tileMap == null || _tileMap.Length != _wvm.PixelMap.ColorBuffers.Length)
                    {
                        _tileMap = new Texture2D[_wvm.PixelMap.ColorBuffers.Length];
                    }

                    for (int i = 0; i < _tileMap.Length; i++)
                    {
                        if (!Check2DFrustrum(i))
                            continue;

                        // Make a new texture for nulls
                        bool init = _tileMap[i] == null;
                        if (init)
                            _tileMap[i] = new Texture2D(e.GraphicsDevice, _wvm.PixelMap.TileWidth, _wvm.PixelMap.TileHeight);

                        if (_wvm.PixelMap.BufferUpdated[i] || init)
                        {
                            _tileMap[i].SetData(_wvm.PixelMap.ColorBuffers[i]);
                            _wvm.PixelMap.BufferUpdated[i] = false;
                        }
                    }
                }
            }
        }

        private void DrawSprites()
        {
            if (!_textureDictionary.Valid)
                return;
        }

        private void DrawPixelTiles()
        {
            //for (int i = 0; i < tileMap.Length; i++)
            //    tileMap[i].SetData<UInt32>(colors, i * tileWidth * tileHeight, tileWidth * tileHeight);
            if (_tileMap == null)
                return;

            for (int i = 0; i < _tileMap.Length; i++)
            {
                if (!Check2DFrustrum(i))
                    continue;

                _spriteBatch.Draw(
                    _tileMap[i],
                    TileOrigin(
                    (i % _wvm.PixelMap.TilesX) * _wvm.PixelMap.TileWidth,
                    (i / _wvm.PixelMap.TilesX) * _wvm.PixelMap.TileHeight),
                    null,
                    Color.White,
                    0,
                    Vector2.Zero,
                    _zoom,
                    SpriteEffects.None,
                    0);
            }
        }

        private void DrawToolPreview()
        {
            if (_preview == null)
                return;
            Vector2 position = new Vector2(0, 0);

            if (_wvm.ActiveTool.ToolType == ToolType.Brush)
            {
                position = new Vector2((_scrollPosition.X + _wvm.MouseOverTile.MouseState.Location.X - _wvm.Brush.OffsetX) * _zoom,
                                       (_scrollPosition.Y + _wvm.MouseOverTile.MouseState.Location.Y - _wvm.Brush.OffsetY) * _zoom);
            }
            else
            {
                position = new Vector2((_scrollPosition.X + _wvm.MouseOverTile.MouseState.Location.X) * _zoom,
                                       (_scrollPosition.Y + _wvm.MouseOverTile.MouseState.Location.Y) * _zoom);

            }

            _spriteBatch.Draw(
                _preview,
                position,
                null,
                Color.White,
                0,
                Vector2.Zero,
                _zoom,
                SpriteEffects.None,
                0);
        }

        private Vector2 TileOrigin(int tileX, int tileY)
        {
            return new Vector2(
                (_scrollPosition.X + tileX) * _zoom,
                (_scrollPosition.Y + tileY) * _zoom);
        }

        private bool Check2DFrustrum(int tileIndex)
        {
            int x = tileIndex % _wvm.PixelMap.TilesX;
            // X off min side
            var xmin = (int)(-_scrollPosition.X / _wvm.PixelMap.TileWidth);
            if (x < xmin)
                return false;

            // x off max side
            if (x > 1 + xmin + (int)((xnaViewport.GraphicsService.GraphicsDevice.Viewport.Width / _zoom) / _wvm.PixelMap.TileWidth))
                return false;


            int y = tileIndex / _wvm.PixelMap.TilesX;

            var ymin = (int)(-_scrollPosition.Y / _wvm.PixelMap.TileHeight);
            if (y < ymin)
                return false;

            if (y > 1 + ymin + (int)((xnaViewport.GraphicsService.GraphicsDevice.Viewport.Height / _zoom) / _wvm.PixelMap.TileHeight))
                return false;

            return true;
        }

        private void xnaViewport_SizeChanged(object sender, SizeChangedEventArgs e)
        {
        }

        #endregion

        #region Mouse

        private TileMouseState GetTileMouseState(HwndMouseEventArgs e)
        {
            return TileMouseState.FromHwndMouseEventArgs(e,
                                                         new Vector2Int32(
                                                             (int)MathHelper.Clamp((float)(e.Position.X / _zoom - _scrollPosition.X), 0, _wvm.CurrentWorld.TilesWide - 1),
                                                             (int)MathHelper.Clamp((float)(e.Position.Y / _zoom - _scrollPosition.Y), 0, _wvm.CurrentWorld.TilesHigh - 1)));
        }

        private void xnaViewport_HwndMouseMove(object sender, HwndMouseEventArgs e)
        {
            _mousePosition = PointToVector2(e.Position);
            if (_wvm.CurrentWorld != null)
                _wvm.MouseMoveTile(GetTileMouseState(e));
        }

        private void xnaViewport_HwndLButtonDown(object sender, HwndMouseEventArgs e)
        {
            if (_wvm.CurrentWorld != null)
                _wvm.MouseDownTile(GetTileMouseState(e));
        }

        private void xnaViewport_HwndLButtonUp(object sender, HwndMouseEventArgs e)
        {
            if (_wvm.CurrentWorld != null)
                _wvm.MouseUpTile(GetTileMouseState(e));
        }

        private void xnaViewport_HwndRButtonDown(object sender, HwndMouseEventArgs e)
        {
            if (_wvm.CurrentWorld != null)
                _wvm.MouseDownTile(GetTileMouseState(e));
        }

        private void xnaViewport_HwndRButtonUp(object sender, HwndMouseEventArgs e)
        {
            if (_wvm.CurrentWorld != null)
                _wvm.MouseUpTile(GetTileMouseState(e));
        }

        private void xnaViewport_HwndMouseWheel(object sender, HwndMouseEventArgs e)
        {
            int x = e.WheelDelta;
            float tempZoom = _zoom;
            if (x > 0)
                tempZoom = _zoom * 2F;
            if (x < 0)
                tempZoom = _zoom / 2F;
            Vector2Int32 curTile = _wvm.MouseOverTile.MouseState.Location;
            _zoom = MathHelper.Clamp(tempZoom, 0.125F, 64F);
            CenterOnTile(curTile.X, curTile.Y);
        }

        private void xnaViewport_HwndMButtonDown(object sender, HwndMouseEventArgs e)
        {
            _middleClickPoint = PointToVector2(e.Position);
            _isMiddleMouseDown = true;
        }

        private void xnaViewport_HwndMButtonUp(object sender, HwndMouseEventArgs e)
        {
            _isMiddleMouseDown = false;
        }

        private void xnaViewport_HwndMouseEnter(object sender, HwndMouseEventArgs e)
        {
        }

        private void xnaViewport_HwndMouseLeave(object sender, HwndMouseEventArgs e)
        {
        }

        #endregion
    }
}