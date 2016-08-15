﻿using MyerSplash.Common;
using MyerSplash.Model;
using MyerSplash.UC;
using MyerSplash.ViewModel;
using SamplesCommon;
using System;
using System.Diagnostics;
using System.Numerics;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace MyerSplash.View
{
    public sealed partial class MainPage : BindablePage
    {
        private MainViewModel MainVM { get; set; }

        private Compositor _compositor;
        private Visual _loadingVisual;
        private Visual _refreshVisual;
        private Visual _drawerVisual;
        private Visual _drawerMaskVisual;
        private Visual _titleGridVisual;
        private Visual _refreshBtnVisual;
        private Visual _rootVisual;

        private double _lastVerticalOffset = 0;
        private bool _isHideTitleGrid = false;

        private UnsplashImage _clickedImg;
        private FrameworkElement _clickedContainer;
        private bool _waitForToggleDetailAnimation;

        public bool IsLoading
        {
            get { return (bool)GetValue(IsLoadingProperty); }
            set { SetValue(IsLoadingProperty, value); }
        }

        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register("IsLoading", typeof(bool), typeof(MainViewModel), 
                new PropertyMetadata(false, OnLoadingPropertyChanged));

        public static void OnLoadingPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var page = d as MainPage;
            if (!(bool)e.NewValue)
            {
                page.HideLoading();
            }
            else page.ShowLoading();
        }

        public bool DrawerOpended
        {
            get { return (bool)GetValue(DrawerOpendedProperty); }
            set { SetValue(DrawerOpendedProperty, value); }
        }

        public static readonly DependencyProperty DrawerOpendedProperty =
            DependencyProperty.Register("DrawerOpended", typeof(bool), typeof(MainPage), 
                new PropertyMetadata(false, OnDrawerOpenedPropertyChanged));

        public static void OnDrawerOpenedPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var page = d as MainPage;
            page.ToggleDrawerAnimation((bool)e.NewValue);
            page.ToggleDrawerMaskAnimation((bool)e.NewValue);
        }

        public MainPage()
        {
            this.InitializeComponent();
            this.DataContext = MainVM = new MainViewModel();
            this.SizeChanged += MainPage_SizeChanged;
            this.Loaded += MainPage_Loaded;

            InitComposition();
            InitBinding();

            var titleBarUC = new EmptyTitleControl();
            (this.Content as Grid).Children.Add(titleBarUC);
            Grid.SetColumnSpan(titleBarUC, 5);
            Grid.SetRowSpan(titleBarUC, 5);
            Canvas.SetZIndex(titleBarUC, 100);

            Window.Current.SetTitleBar(titleBarUC);
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            _loadingVisual.Offset = new Vector3(0f, -70f, 0f);
            _drawerMaskVisual.Opacity = 0;
            _drawerVisual.Offset = new Vector3(-300f, 0f, 0f);

            DrawerMaskBorder.Visibility = Visibility.Collapsed;
        }

        private void MainPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!DrawerOpended)
            {
                _drawerVisual.Offset = new Vector3(-(float)Window.Current.Bounds.Width, 0f, 0f);
            }
        }

        private void InitBinding()
        {
            var b = new Binding()
            {
                Source = MainVM,
                Path = new PropertyPath("IsRefreshing"),
                Mode = BindingMode.TwoWay,
            };
            this.SetBinding(IsLoadingProperty, b);

            var b2 = new Binding()
            {
                Source = MainVM,
                Path = new PropertyPath("DrawerOpened"),
                Mode = BindingMode.TwoWay,
            };
            this.SetBinding(DrawerOpendedProperty, b2);
        }

        private void InitComposition()
        {
            _compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
            _rootVisual = ElementCompositionPreview.GetElementVisual(this);
            _loadingVisual = ElementCompositionPreview.GetElementVisual(LoadingGrid);
            _refreshVisual = ElementCompositionPreview.GetElementVisual(RefreshIcon);
            _drawerVisual = ElementCompositionPreview.GetElementVisual(DrawerControl);
            _drawerMaskVisual = ElementCompositionPreview.GetElementVisual(DrawerMaskBorder);
            _titleGridVisual = ElementCompositionPreview.GetElementVisual(TitleGrid);
            _refreshBtnVisual = ElementCompositionPreview.GetElementVisual(RefreshBtn);

            SurfaceLoader.Initialize(_compositor);
        }

        #region Loading animation
        private void ShowLoading()
        {
            var showAnimation = _compositor.CreateScalarKeyFrameAnimation();
            showAnimation.InsertKeyFrame(1f,80f);
            showAnimation.Duration = TimeSpan.FromMilliseconds(500);

            var rotateAnimation = _compositor.CreateScalarKeyFrameAnimation();
            rotateAnimation.InsertKeyFrame(1, 3600f, _compositor.CreateLinearEasingFunction());
            rotateAnimation.Duration = TimeSpan.FromMilliseconds(10000);
            rotateAnimation.IterationBehavior = AnimationIterationBehavior.Forever;

            _loadingVisual.IsVisible = true;
            _refreshVisual.CenterPoint = new Vector3((float)LoadingGrid.ActualWidth / 2, (float)LoadingGrid.ActualHeight / 2, 0f);
            _refreshVisual.RotationAngleInDegrees = 0;

            _refreshVisual.StopAnimation("RotationAngleInDegrees");
            _refreshVisual.StartAnimation("RotationAngleInDegrees", rotateAnimation);
            _loadingVisual.StartAnimation("Offset.y", showAnimation);
        }

        private void HideLoading()
        {
            var showAnimation = _compositor.CreateScalarKeyFrameAnimation();
            showAnimation.InsertKeyFrame(1, -80f);
            showAnimation.Duration = TimeSpan.FromMilliseconds(500);

            var batch = _compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            _loadingVisual.StartAnimation("Offset.y", showAnimation);
            batch.Completed += (sender, e) =>
              {
                  _loadingVisual.IsVisible = false;
              };
            batch.End();
        }
        #endregion

        #region Drawer animation
        private void ToggleDrawerAnimation(bool show)
        {
            var offsetAnim = _compositor.CreateScalarKeyFrameAnimation();
            offsetAnim.InsertKeyFrame(1f, show ? 0f : -300);
            offsetAnim.Duration = TimeSpan.FromMilliseconds(300);

            _drawerVisual.StartAnimation("Offset.X", offsetAnim);
        }

        private void ToggleDrawerMaskAnimation(bool show)
        {
            if (show) DrawerMaskBorder.Visibility = Visibility.Visible;

            var fadeAnimation = _compositor.CreateScalarKeyFrameAnimation();
            fadeAnimation.InsertKeyFrame(1f, show ? 1f : 0f, _compositor.CreateLinearEasingFunction());
            fadeAnimation.Duration = TimeSpan.FromMilliseconds(300);

            var batch = _compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            _drawerMaskVisual.StartAnimation("Opacity", fadeAnimation);
            batch.Completed += (sender, e) =>
              {
                  if (!show) DrawerMaskBorder.Visibility = Visibility.Collapsed;
              };
            batch.End();
        }
        #endregion

        private void StackPanel_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ListControl.ScrollToTop();
        }

        private void DetailControl_OnHideControl()
        {
            ListControl.HideItemDetailAnimation();
        }

        private void ListControl_OnClickItemStarted(UnsplashImage img, FrameworkElement container)
        {
            _clickedContainer = container;
            _clickedImg = img;

            DetailControl.Visibility = Visibility.Visible;
            if (DetailControl.ActualHeight == 0)
            {
                _waitForToggleDetailAnimation = true;
            }
            else
            {
                _waitForToggleDetailAnimation = false;
                ToggleDetailControlAnimation();
            }
        }

        private void DetailControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_waitForToggleDetailAnimation)
            {
                _waitForToggleDetailAnimation = false;

                ToggleDetailControlAnimation();
            }
        }

        private void ToggleDetailControlAnimation()
        {
            DetailControl.CurrentImage = _clickedImg;

            var currentPos = _clickedContainer.TransformToVisual(ListControl).TransformPoint(new Point(0, 0));
            var targetPos = GetTargetPosition();
            var targetRatio = GetTargetSize().X / _clickedContainer.ActualWidth;
            var targetOffsetX = targetPos.X - currentPos.X;
            var targetOffsetY = targetPos.Y - currentPos.Y;

            ListControl.MoveItemAnimation(new Vector3((float)targetOffsetX, (float)targetOffsetY, 0f), (float)targetRatio);
            DetailControl.ToggleDetailGridAnimation(true);

            NavigationService.HistoryOperationsBeyondFrame.Push(() =>
            {
                var content = Frame.Content;
                if (content.GetType() == typeof(MainPage))
                {
                    DetailControl.HideDetailControl();
                    return true;
                }
                else return false;
            });
        }

        private Vector2 GetTargetPosition()
        {
            var size = GetTargetSize();

            var x = 0f;
            var y = 0f;
            if(size.X!= this.ActualWidth)
            {
                x = (float)(this.ActualWidth - size.X) / 2f;
            }
            y = (float)(this.ActualHeight - size.Y) / 2f;

            return new Vector2(x, y);
        }

        private Vector2 GetTargetSize()
        {
            var width = Math.Min(600, this.ActualWidth);
            var height = width / 1.5 + 100;

            return new Vector2((float)width, (float)height);
        }

        private void DetailControl_Loaded(object sender, RoutedEventArgs e)
        {
            DetailControl.Visibility = Visibility.Collapsed;
            DetailControl.SizeChanged -= DetailControl_SizeChanged;
            DetailControl.SizeChanged += DetailControl_SizeChanged;
        }

        #region Scrolling
        private void ToggleTitleBarAnimation(bool show)
        {
            var offsetAnimation = _compositor.CreateScalarKeyFrameAnimation();
            offsetAnimation.InsertKeyFrame(1f, show ? 0f : -100f);
            offsetAnimation.Duration = TimeSpan.FromMilliseconds(500);

            //_titleGridVisual.StartAnimation("Offset.Y", offsetAnimation);
        }

        private void ToggleRefreshBtnAnimation(bool show)
        {
            Debug.WriteLine(_refreshBtnVisual.Offset);

            _refreshBtnVisual.CenterPoint = new Vector3((float)RefreshBtn.ActualWidth/2f, (float)RefreshBtn.ActualHeight/2f, 0f);
            var scaleAnimation = _compositor.CreateVector3KeyFrameAnimation();
            scaleAnimation.InsertKeyFrame(1f, show ? new Vector3(1f, 1f, 1f) : new Vector3(0f, 0f, 0f));
            scaleAnimation.Duration = TimeSpan.FromMilliseconds(500);
            _refreshBtnVisual.StartAnimation("Scale", scaleAnimation);

            //var offsetAnimation = _compositor.CreateScalarKeyFrameAnimation();
            //offsetAnimation.InsertKeyFrame(1f,!show ? _rootVisual.Size.Y:_rootVisual.Size.Y-70f);
            //offsetAnimation.SetVector3Parameter("offset", _refreshBtnVisual.Offset);
            //offsetAnimation.Duration = TimeSpan.FromMilliseconds(500);
            //_refreshBtnVisual.StartAnimation("Offset.Y", offsetAnimation);

        }

        private void ListControl_OnScrollViewerViewChanged(ScrollViewer scrollViewer)
        {
            if ((scrollViewer.VerticalOffset - _lastVerticalOffset) > 30 && !_isHideTitleGrid)
            {
                _isHideTitleGrid = true;
                ToggleRefreshBtnAnimation(false);
                ToggleTitleBarAnimation(false);
            }
            else if (scrollViewer.VerticalOffset < _lastVerticalOffset && _isHideTitleGrid)
            {
                _isHideTitleGrid = false;
                ToggleRefreshBtnAnimation(true);
                ToggleTitleBarAnimation(true);
            }
            _lastVerticalOffset = scrollViewer.VerticalOffset;
        }
        #endregion

        #region Drawer manipulation
        private void TouchGrid_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            if (e.Cumulative.Translation.X >= 70)
            {
                if (!DrawerOpended)
                {
                    DrawerOpended = true;
                }
                else
                {
                    ToggleDrawerAnimation(true);
                    ToggleDrawerMaskAnimation(true);
                }

                NavigationService.HistoryOperationsBeyondFrame.Push(() =>
                {
                    var content = Frame.Content;
                    if (content.GetType() == typeof(MainPage))
                    {
                        MainVM.DrawerOpened = false;
                        return true;
                    }
                    else return false;
                });
            }
            else
            {
                if (DrawerOpended)
                {
                    DrawerOpended = false;
                }
                else
                {
                    ToggleDrawerAnimation(false);
                    ToggleDrawerMaskAnimation(false);
                }
            }
        }

        private void TouchGrid_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            if (_drawerMaskVisual.Opacity < 1)
            {
                DrawerMaskBorder.Visibility = Visibility.Visible;
                _drawerMaskVisual.Opacity += 0.02f;
            }
            var targetOffsetX = _drawerVisual.Offset.X + e.Delta.Translation.X;
            _drawerVisual.Offset = new Vector3((float)(targetOffsetX > 1 ? 1 : targetOffsetX), 0f, 0f);
        }

        private void DrawerControl_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            if (e.Delta.Translation.X > 0) return;

            if (_drawerMaskVisual.Opacity > 0)
            {
                _drawerMaskVisual.Opacity -= 0.01f;
            }
            var targetOffsetX = _drawerVisual.Offset.X - Math.Abs(e.Delta.Translation.X);
            _drawerVisual.Offset = new Vector3((float)(targetOffsetX <= -300f ? -300f : targetOffsetX), 0f, 0f);
        }

        private void DrawerControl_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            //DrawerMaskBorder.Visibility = Visibility.Collapsed;

            if (e.Cumulative.Translation.X <= -30)
            {
                DrawerOpended = false;
            }
            else
            {

            }
        }
        #endregion

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            TitleBarHelper.SetUpBlackTitleBar();
        }
    }
}
