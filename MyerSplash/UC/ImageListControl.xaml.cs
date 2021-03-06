﻿using CompositionHelper.Animation.Fluent;
using JP.Utils.UI;
using MyerSplash.Model;
using MyerSplash.ViewModel;
using System;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace MyerSplash.UC
{
    public sealed partial class ImageListControl : UserControl
    {
        public event Action<UnsplashImageBase, FrameworkElement> OnClickItemStarted;
        public event Action<ScrollViewer> OnScrollViewerViewChanged;

        private MainViewModel MainVM
        {
            get
            {
                return this.DataContext as MainViewModel;
            }
        }

        private Visual _containerVisual;
        private Visual _listVisual;
        private Compositor _compositor;

        private int _zindex = 1;

        public int TargetOffsetX;
        public int TargetOffsetY;

        private ScrollViewer _scrollViewer;

        public bool Refreshing
        {
            get { return (bool)GetValue(MyPropertyProperty); }
            set { SetValue(MyPropertyProperty, value); }
        }

        public static readonly DependencyProperty MyPropertyProperty =
            DependencyProperty.Register("Refreshing", typeof(bool), typeof(ImageListControl),
                new PropertyMetadata(false, OnRefreshingPropertyChanged));

        private static void OnRefreshingPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as ImageListControl;
            if ((bool)e.NewValue)
            {
                control.ShowRefreshing();
            }
            else control.HideRefreshing();
        }

        public ImageListControl()
        {
            this.InitializeComponent();
            this._compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
            this._listVisual = ElementCompositionPreview.GetElementVisual(ImageGridView);
        }

        private void ImageGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem;
            var container = ImageGridView.ContainerFromItem(item) as FrameworkElement;
            var rootGrid = (container as GridViewItem).ContentTemplateRoot as Grid;
            Canvas.SetZIndex(container, ++_zindex);

            _containerVisual = ElementCompositionPreview.GetElementVisual(container);

            var unsplashImg = item as UnsplashImageBase;

            var maskBorder = rootGrid.Children[2] as FrameworkElement;
            var img = rootGrid.Children[1] as FrameworkElement;

            ToggleItemPointAnimation(maskBorder, img, false);

            OnClickItemStarted?.Invoke(unsplashImg, container);
        }

        public void MoveItemAnimation(Vector3 targetOffset, float widthRatio)
        {
            var offsetAnimation = _compositor.CreateVector3KeyFrameAnimation();
            offsetAnimation.InsertKeyFrame(1f, targetOffset);
            offsetAnimation.Duration = TimeSpan.FromMilliseconds(500);

            var scaleAnimation = _compositor.CreateScalarKeyFrameAnimation();
            scaleAnimation.InsertKeyFrame(1f, widthRatio);
            scaleAnimation.Duration = TimeSpan.FromMilliseconds(500);

            var fadeAnimation = _compositor.CreateScalarKeyFrameAnimation();
            fadeAnimation.InsertKeyFrame(1f, 0.5f);
            fadeAnimation.Duration = TimeSpan.FromMilliseconds(500);

            _containerVisual.StartAnimation("Offset", offsetAnimation);
            _containerVisual.StartAnimation("Scale.x", scaleAnimation);
            _containerVisual.StartAnimation("Scale.y", scaleAnimation);
        }

        public void HideItemDetailAnimation()
        {
            var offsetAnimation = _compositor.CreateVector3KeyFrameAnimation();
            offsetAnimation.InsertKeyFrame(1f, new Vector3(0, 0, 0));
            offsetAnimation.Duration = TimeSpan.FromMilliseconds(500);

            var scaleAnimation = _compositor.CreateScalarKeyFrameAnimation();
            scaleAnimation.InsertKeyFrame(1f, 1f);
            scaleAnimation.Duration = TimeSpan.FromMilliseconds(500);

            _containerVisual.StartAnimation("Offset", offsetAnimation);
            _containerVisual.StartAnimation("Scale.x", scaleAnimation);
            _containerVisual.StartAnimation("Scale.y", scaleAnimation);
        }

        public void ScrollToTop()
        {
            ImageGridView.GetScrollViewer().ChangeView(null, 0, null);
        }

        public void ShowLoadingAnimation()
        {
            ScrollToTop();
            ToggleLoadingAnimation(true);
        }

        public void HideLoadingAnimation()
        {
            ToggleLoadingAnimation(false);
        }

        private void ToggleLoadingAnimation(bool show)
        {
            var offsetAnimation = _compositor.CreateVector3KeyFrameAnimation();
            offsetAnimation.InsertKeyFrame(1, new Vector3(0f, show ? 70f : 0f, 0f));
            offsetAnimation.Duration = TimeSpan.FromMilliseconds(500);

            _listVisual.StartAnimation("Offset", offsetAnimation);
        }

        #region List Animation
        private void AdaptiveGridView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            int index = args.ItemIndex;

            if (!args.InRecycleQueue)
            {
                args.ItemContainer.Loaded -= ItemContainer_Loaded;
                args.ItemContainer.Loaded += ItemContainer_Loaded;
            }
        }

        private void ItemContainer_Loaded(object sender, RoutedEventArgs e)
        {
            var itemsPanel = (ItemsWrapGrid)ImageGridView.ItemsPanelRoot;
            var itemContainer = (GridViewItem)sender;
            var itemIndex = ImageGridView.IndexFromContainer(itemContainer);

            // Don't animate if we're not in the visible viewport
            if (itemIndex >= itemsPanel.FirstVisibleIndex && itemIndex <= itemsPanel.LastVisibleIndex)
            {
                var itemVisual = ElementCompositionPreview.GetElementVisual(itemContainer);
                var delayIndex = itemIndex - itemsPanel.FirstVisibleIndex;

                itemVisual.Opacity = 0f;
                itemVisual.Offset = new Vector3(50, 0, 0);

                // Create KeyFrameAnimations
                var offsetAnimation = _compositor.CreateScalarKeyFrameAnimation();
                offsetAnimation.InsertExpressionKeyFrame(1f, "0");
                offsetAnimation.Duration = TimeSpan.FromMilliseconds(700);
                offsetAnimation.DelayTime = TimeSpan.FromMilliseconds((delayIndex * 100));

                var fadeAnimation = _compositor.CreateScalarKeyFrameAnimation();
                fadeAnimation.InsertExpressionKeyFrame(1f, "1");
                fadeAnimation.Duration = TimeSpan.FromMilliseconds(700);
                fadeAnimation.DelayTime = TimeSpan.FromMilliseconds(delayIndex * 100);

                // Start animations
                itemVisual.StartAnimation("Offset.X", offsetAnimation);
                itemVisual.StartAnimation("Opacity", fadeAnimation);
            }
            itemContainer.Loaded -= ItemContainer_Loaded;
        }
        #endregion

        private void ImageGridView_Loaded(object sender, RoutedEventArgs e)
        {
            _scrollViewer = ImageGridView.GetScrollViewer();
            _scrollViewer.ViewChanging -= ScrollViewer_ViewChanging;
            _scrollViewer.ViewChanging += ScrollViewer_ViewChanging;

        }

        private void ScrollViewer_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
        {
            OnScrollViewerViewChanged?.Invoke(sender as ScrollViewer);
        }

        private void RootGrid_Loaded(object sender, RoutedEventArgs e)
        {
            var rootGrid = sender as Grid;

            rootGrid.PointerEntered += RootGrid_PointerEntered;
            rootGrid.PointerExited += RootGrid_PointerExited;

            var maskBorder = rootGrid.Children[2] as FrameworkElement;
            var maskVisual = ElementCompositionPreview.GetElementVisual(maskBorder);
            maskVisual.Opacity = 0f;
        }

        private void RootGrid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
            {
                return;
            }
            var rootGrid = sender as Grid;
            var maskBorder = rootGrid.Children[2] as FrameworkElement;
            var img = rootGrid.Children[1] as FrameworkElement;

            ToggleItemPointAnimation(maskBorder, img, false);
        }

        private void RootGrid_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
            {
                return;
            }
            var rootGrid = sender as Grid;
            var maskBorder = rootGrid.Children[2] as FrameworkElement;
            var img = rootGrid.Children[1] as FrameworkElement;

            ToggleItemPointAnimation(maskBorder, img, true);
        }

        private void RootGrid_Unloaded(object sender, RoutedEventArgs e)
        {
            var rootGrid = sender as Grid;
            rootGrid.PointerEntered -= RootGrid_PointerEntered;
            rootGrid.PointerExited -= RootGrid_PointerExited;
        }

        private ScalarKeyFrameAnimation CreateScaleAnimation(bool show)
        {
            var scaleAnimation = _compositor.CreateScalarKeyFrameAnimation();
            scaleAnimation.InsertKeyFrame(1f, show ? 1.1f : 1f);
            scaleAnimation.Duration = TimeSpan.FromMilliseconds(1000);
            scaleAnimation.StopBehavior = AnimationStopBehavior.LeaveCurrentValue;
            return scaleAnimation;
        }

        private ScalarKeyFrameAnimation CreateFadeAnimation(bool show)
        {
            var fadeAnimation = _compositor.CreateScalarKeyFrameAnimation();
            fadeAnimation.InsertKeyFrame(1f, show ? 1f : 0f);
            fadeAnimation.Duration = TimeSpan.FromMilliseconds(500);

            return fadeAnimation;
        }

        private void ToggleItemPointAnimation(FrameworkElement mask, FrameworkElement img, bool show)
        {
            var maskVisual = ElementCompositionPreview.GetElementVisual(mask);
            var imgVisual = ElementCompositionPreview.GetElementVisual(img);

            var fadeAnimation = CreateFadeAnimation(show);
            var scaleAnimation = CreateScaleAnimation(show);

            if (imgVisual.CenterPoint.X == 0 && imgVisual.CenterPoint.Y == 0)
            {
                imgVisual.CenterPoint = new Vector3((float)img.ActualHeight / 2, (float)img.ActualHeight / 2, 0f);
            }

            maskVisual.StartAnimation("Opacity", fadeAnimation);
            imgVisual.StartAnimation("Scale.x", scaleAnimation);
            imgVisual.StartAnimation("Scale.y", scaleAnimation);
        }

        private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var rootGrid = sender as Grid;
            rootGrid.Clip = new RectangleGeometry()
            {
                Rect = new Rect(0, 0, rootGrid.ActualWidth, rootGrid.ActualHeight)
            };
        }

        private void ShowRefreshing()
        {
            if (_scrollViewer != null)
            {
                _scrollViewer.ChangeView(null, 0, null);
            }
            var offsetAnimation = _compositor.CreateScalarKeyFrameAnimation();
            offsetAnimation.InsertKeyFrame(1f, 70f);
            offsetAnimation.Duration = TimeSpan.FromMilliseconds(300);

            _listVisual.StartAnimation("Offset.y", offsetAnimation);
            LoadingControl.Visibility = Visibility.Visible;
            LoadingControl.Start();
        }

        private void HideRefreshing()
        {
            var offsetAnimation = _compositor.CreateScalarKeyFrameAnimation();
            offsetAnimation.InsertKeyFrame(1f, 0f);
            offsetAnimation.Duration = TimeSpan.FromMilliseconds(300);

            _listVisual.StartAnimation("Offset.y", offsetAnimation);
            LoadingControl.Visibility = Visibility.Collapsed;
            LoadingControl.Stop();
        }
    }
}
