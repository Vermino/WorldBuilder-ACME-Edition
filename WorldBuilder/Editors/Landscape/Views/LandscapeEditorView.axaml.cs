using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WorldBuilder.Editors.Landscape.ViewModels;
using WorldBuilder.Lib;
using WorldBuilder.Lib.Docking;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace WorldBuilder.Editors.Landscape.Views {
    public partial class LandscapeEditorView : UserControl {
        private LandscapeEditorViewModel? _viewModel;
        private Avalonia.Controls.Grid? _mainGrid;
        private readonly Dictionary<IDockable, Window> _floatingWindows = new();

        public LandscapeEditorView() {
            InitializeComponent();

            if (Design.IsDesignMode) return;

            _viewModel = ProjectManager.Instance.GetProjectService<LandscapeEditorViewModel>()
                ?? throw new Exception("Failed to get LandscapeEditorViewModel");

            DataContext = _viewModel;

            _mainGrid = this.FindControl<Avalonia.Controls.Grid>("MainGrid");

            // Restore panel widths
            var uiState = _viewModel.Settings.Landscape.UIState;
            if (uiState != null && _mainGrid != null) {
                if (uiState.LeftPanelWidth > 0 && _mainGrid.ColumnDefinitions.Count > 0)
                    _mainGrid.ColumnDefinitions[0].Width = new Avalonia.Controls.GridLength(uiState.LeftPanelWidth);
                if (uiState.RightPanelWidth > 0 && _mainGrid.ColumnDefinitions.Count > 4)
                    _mainGrid.ColumnDefinitions[4].Width = new Avalonia.Controls.GridLength(uiState.RightPanelWidth);
            }

            // Setup floating windows
            if (_viewModel.DockingManager != null) {
                _viewModel.DockingManager.FloatingPanels.CollectionChanged += OnFloatingPanelsChanged;
                // Just in case panels were added before we subscribed (unlikely if Init is called later)
                foreach (var panel in _viewModel.DockingManager.FloatingPanels) {
                    CreateFloatingWindow(panel);
                }
            }

            // Initialize ViewModel if needed
            if (ProjectManager.Instance.CurrentProject != null) {
                // Ensure Init is called. In the old code it was lazy in OnGlRender.
                // We call it here. Use a flag in ViewModel if Init is idempotent or check TerrainSystem.
                if (_viewModel.TerrainSystem == null) {
                    _viewModel.Init(ProjectManager.Instance.CurrentProject);
                }
            }
        }

        private void OnFloatingPanelsChanged(object? sender, NotifyCollectionChangedEventArgs e) {
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null) {
                foreach (IDockable panel in e.NewItems) {
                    CreateFloatingWindow(panel);
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null) {
                foreach (IDockable panel in e.OldItems) {
                    if (_floatingWindows.TryGetValue(panel, out var window)) {
                        // Close window if it's open (and not already closing)
                        // Note: Avalonia Window.Close() is safe to call multiple times usually
                        window.Close();
                        _floatingWindows.Remove(panel);
                    }
                }
            }
            // Handle Reset?
             else if (e.Action == NotifyCollectionChangedAction.Reset) {
                 foreach(var window in _floatingWindows.Values) {
                     window.Close();
                 }
                 _floatingWindows.Clear();
             }
        }

        private void CreateFloatingWindow(IDockable panel) {
            if (_floatingWindows.ContainsKey(panel)) return;

            var window = new Window {
                Title = panel.Title,
                Content = panel, // Use the panel VM as content so DataTemplate applies
                Width = 300,
                Height = 400,
                ShowInTaskbar = true
            };

            window.Closing += (s, e) => {
                if (panel.Location == DockLocation.Floating) {
                    // If closed by user, hide the panel
                    // We must ensure this doesn't cause loop if triggered by Remove
                    if (_floatingWindows.ContainsKey(panel)) {
                         panel.IsVisible = false;
                    }
                }
            };

            window.Show();
            _floatingWindows[panel] = window;
        }

        private void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e) {
            base.OnDetachedFromVisualTree(e);

            // Close all floating windows
            foreach(var window in _floatingWindows.Values) {
                window.Close();
            }
            _floatingWindows.Clear();

            // Save panel widths
            var uiState = _viewModel?.Settings.Landscape.UIState;
            if (uiState != null && _mainGrid != null) {
                try {
                    if (_mainGrid.ColumnDefinitions.Count > 0)
                        uiState.LeftPanelWidth = _mainGrid.ColumnDefinitions[0].ActualWidth;
                    if (_mainGrid.ColumnDefinitions.Count > 4)
                        uiState.RightPanelWidth = _mainGrid.ColumnDefinitions[4].ActualWidth;
                }
                catch { }
            }

            _viewModel?.Cleanup();
        }
    }
}
