﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;

namespace System.CommandLine.Rendering.Views
{
    public class StackLayoutView : LayoutView<View>
    {
        public StackLayoutView()
                : this(Orientation.Vertical)
        {
        }

        public StackLayoutView(Orientation orientation)
        {
            Orientation = orientation;
        }

        public Orientation Orientation { get; }

        public override void Render(ConsoleRenderer renderer, Region region = null)
        {
            switch (Orientation)
            {
                case Orientation.Vertical:
                    RenderVertical(region, renderer);
                    break;
                case Orientation.Horizontal:
                    RenderHorizontal(region, renderer);
                    break;
            }
        }

        protected virtual void RenderVertical(Region region, ConsoleRenderer renderer)
        {
            var left = region.Left;
            var top = region.Top;
            var height = region.Height;

            foreach (var child in Children)
            {
                if (height <= 0)
                {
                    break;
                }
                var size = child.Measure(renderer, new Size(region.Width, height));
                int renderHeight = Math.Min(height, size.Height);
                var r = new Region(left, top, size.Width, renderHeight);
                child.Render(renderer, r);
                top += size.Height;
                height -= renderHeight;
            }
        }

        private void RenderHorizontal(Region region, ConsoleRenderer renderer)
        {
            var left = region.Left;
            var top = region.Top;
            var width = region.Width;

            foreach (var child in Children)
            {
                if (width <= 0)
                {
                    break;
                }
                var size = child.Measure(renderer, new Size(width, region.Height));
                var r = new Region(left, top, width, size.Height);
                child.Render(renderer, r);
                left += size.Width;
                width -= size.Width;
            }
        }

        public override Size Measure(ConsoleRenderer renderer, Size maxSize)
        {
            switch (Orientation)
            {
                case Orientation.Vertical:
                    return GetAdjustedSizeVertical(renderer, maxSize);
                case Orientation.Horizontal:
                    return GetAdjustedSizeHorizontal(renderer, maxSize);
                default:
                    throw new InvalidOperationException($"Orientation {Orientation} is not implemented");
            }
        }

        private Size GetAdjustedSizeVertical(ConsoleRenderer renderer, Size maxSize)
        {
            var maxWidth = 0;
            var totHeight = 0;

            var height = maxSize.Height;

            foreach (var child in Children)
            {
                if (height <= 0)
                {
                    break;
                }
                var size = child.Measure(renderer, new Size(maxSize.Width, height));
                height -= size.Height;
                totHeight += size.Height;
                maxWidth = Math.Max(maxWidth, size.Width);
            }

            return new Size(maxWidth, totHeight);
        }

        private Size GetAdjustedSizeHorizontal(ConsoleRenderer renderer, Size maxSize)
        {
            var maxHeight = 0;
            var totalWidth = 0;

            var width = maxSize.Width;

            foreach (var child in Children)
            {
                if (width <= 0)
                {
                    break;
                }
                var size = child.Measure(renderer, new Size(width, maxSize.Height));
                width -= size.Width;
                totalWidth += size.Width;
                maxHeight = Math.Max(maxHeight, size.Height);
            }

            return new Size(totalWidth, maxHeight);
        }
    }

    public enum ScrollDirection
    {
        Up,
        Down
    }

    public class ScrollableLayoutView : StackLayoutView
    {
        public ScrollDirection ScrollDirection { get; }

        public ScrollableLayoutView(ScrollDirection scrollDirection)
        {
            ScrollDirection = scrollDirection;
        }

        public override IReadOnlyList<View> Children => base.Children.Reverse().ToList().AsReadOnly();

        protected override void RenderVertical(Region region, ConsoleRenderer renderer)
        {
            switch (ScrollDirection)
            {
                case ScrollDirection.Up:
                    RenderVerticalScrollUp(region, renderer);
                    break;
                case ScrollDirection.Down:
                    RenderVerticalScrollDown(region, renderer);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void RenderVerticalScrollUp(Region region, ConsoleRenderer renderer)
        {
            var left = region.Left;
            var top = region.Top;// + region.Height;
            var height = region.Height;

            var renderChildren = new List<(Size size, int renderHeight, View child)>();
            foreach (var child in Children)
            {
                if (height <= 0)
                {
                    break;
                }
                var size = child.Measure(renderer, new Size(region.Width, height));
                int renderHeight = Math.Min(height, size.Height);
                //top -= size.Height;
                height -= renderHeight;
                //var r = new Region(left, top, size.Width, renderHeight);
                //child.Render(renderer, r);
                renderChildren.Add((size, renderHeight, child));
            }

            foreach (var (size, renderHeight, child) in Enumerable.Reverse(renderChildren))
            {
                var r = new Region(left, top, size.Width, renderHeight);
                child.Render(renderer, r);
                top += size.Height;
            }
        }

        private void RenderVerticalScrollDown(Region region, ConsoleRenderer renderer)
        {
            var left = region.Left;
            var top = region.Top;
            var height = region.Height;

            foreach (var child in Children)
            {
                if (height <= 0)
                {
                    break;
                }
                var size = child.Measure(renderer, new Size(region.Width, height));
                int renderHeight = Math.Min(height, size.Height);
                var r = new Region(left, top, size.Width, renderHeight);
                top += size.Height;
                height -= renderHeight;
                child.Render(renderer, r);
            }
        }

        //public override void Add(View child)
        //{
        //    base.Add(child);
        //    OnUpdated();
        //}

        //public override bool Remove(View child)
        //{
        //    var removed = base.Remove(child);
        //    if (removed)
        //    {
        //        OnUpdated();
        //    }

        //    return removed;
        //}

        // FromObservable fuer die Children, aber update ist hier add item, nicht content children changed

        protected void Observe<T>(IObservable<T> observable, Func<T, View> viewProvider)
        {
            if (observable == null)
            {
                throw new ArgumentNullException(nameof(observable));
            }

            if (viewProvider == null)
            {
                throw new ArgumentNullException(nameof(viewProvider));
            }

            observable.Subscribe(new Observer<T>(this, viewProvider));
        }

        public static ScrollableLayoutView FromObservable<T>(IObservable<T> observable, ScrollDirection scrollDirection = ScrollDirection.Up, Func<T, View> viewProvider = null)
        {
            var rv = new ScrollableLayoutView(scrollDirection);
            rv.Observe(observable, viewProvider ?? (x => ContentView.Create(x, new TextSpanFormatter())));
            return rv;
        }

        private class Observer<T> : IObserver<T>
        {
            private readonly ScrollableLayoutView _layoutView;
            private readonly Func<T, View> _viewProvider;
            //private readonly TextSpanFormatter _textSpanFormatter = new TextSpanFormatter();

            public Observer(ScrollableLayoutView layoutView, Func<T, View> viewProvider)
            {
                _layoutView = layoutView;
                _viewProvider = viewProvider;
            }

            public void OnCompleted() { }

            public void OnError(Exception error) { }

            public void OnNext(T value)
            {
                _layoutView.Add(_viewProvider(value));
                _layoutView.OnUpdated();
            }
        }
    }
}

