using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Silk.NET.OpenGL;
using System;
using System.Numerics;
using WorldBuilder.Lib;
using WorldBuilder.ViewModels;

namespace WorldBuilder.Views.Components.Viewports {
    public partial class ViewportControl : Base3DView {
        private ViewportViewModel? ViewModel => DataContext as ViewportViewModel;
        private bool _didInit;

        public ViewportControl() {
            InitializeComponent();
            InitializeBase3DView();
        }

        private void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnGlInit(GL gl, PixelSize canvasSize) {
            if (ViewModel != null) {
                // Wait, Renderer property is only set after OnGlInitInternal calls OnGlInit
                // And Renderer is set in Base3DView.OnGlInitInternal BEFORE calling OnGlInit.
                // So Renderer property should be available here.
                ViewModel.Renderer = Renderer;
                _didInit = true;
            }
        }

        protected override void OnGlRender(double deltaTime) {
            if (!_didInit || ViewModel == null) return;
            // Re-set Renderer if needed (e.g. context loss/recreation)
            if (ViewModel.Renderer != Renderer) {
                ViewModel.Renderer = Renderer;
            }

            ViewModel.RenderAction?.Invoke(deltaTime, new PixelSize((int)Bounds.Width, (int)Bounds.Height), InputState);
        }

        protected override void OnGlResize(PixelSize canvasSize) {
            ViewModel?.ResizeAction?.Invoke(canvasSize);
        }

        protected override void OnGlDestroy() {
            if (ViewModel != null) {
                ViewModel.Renderer = null;
            }
        }

        protected override void OnGlKeyDown(KeyEventArgs e) {
            ViewModel?.KeyAction?.Invoke(e, true);
        }

        protected override void OnGlKeyUp(KeyEventArgs e) {
            ViewModel?.KeyAction?.Invoke(e, false);
        }

        protected override void OnGlPointerMoved(PointerEventArgs e, Vector2 mousePositionScaled) {
             ViewModel?.PointerMovedAction?.Invoke(e, mousePositionScaled);
        }

        protected override void OnGlPointerWheelChanged(PointerWheelEventArgs e) {
            ViewModel?.PointerWheelAction?.Invoke(e);
        }

        protected override void OnGlPointerPressed(PointerPressedEventArgs e) {
            ViewModel?.PointerPressedAction?.Invoke(e);
        }

        protected override void OnGlPointerReleased(PointerReleasedEventArgs e) {
            ViewModel?.PointerReleasedAction?.Invoke(e);
        }
    }
}
