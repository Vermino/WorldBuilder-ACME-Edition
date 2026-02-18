using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using WorldBuilder.Editors.Landscape.ViewModels;
using System;

namespace WorldBuilder.Editors.Landscape.Views;

public partial class ObjectBrowserView : UserControl {
    private Point _dragStartPoint;
    private bool _isDragging;

    public ObjectBrowserView() {
        InitializeComponent();
    }

    private void OnItemPointerPressed(object? sender, PointerPressedEventArgs e) {
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed) {
            _dragStartPoint = point.Position;
            _isDragging = false;
        }
    }

    private async void OnItemPointerMoved(object? sender, PointerEventArgs e) {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var point = e.GetCurrentPoint(this);
        var diff = _dragStartPoint - point.Position;

        // Check if drag distance exceeded threshold
        if (!_isDragging && (Math.Abs(diff.X) > 3 || Math.Abs(diff.Y) > 3)) {
            if (sender is Control control && control.DataContext is ObjectBrowserItem item) {
                _isDragging = true;

                var data = new DataObject();
                // Pass the item itself if dragging to internal windows
                // Or pass the ID as text for general purposes
                data.Set("ObjectBrowserItem", item);
                data.Set(DataFormats.Text, item.DisplayId);

                await DragDrop.DoDragDrop(e, data, DragDropEffects.Copy | DragDropEffects.Move);

                _isDragging = false;
            }
        }
    }
}
