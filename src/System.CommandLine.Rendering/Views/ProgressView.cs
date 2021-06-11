using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace System.CommandLine.Rendering.Views
{
    public class ProgressStyleDefinition
    {
        public string BorderLeft { get; set; }
        public string BorderRight { get; set; }
        public ColumnDefinition Size { get; protected set; }
    }

    public class PercentStyleDefinition : ProgressStyleDefinition
    {
        public PercentStyleDefinition(string border) : this(borderLeft: border, borderRight: border)
        { }

        public PercentStyleDefinition(string borderLeft = null, string borderRight = null)
        {
            BorderLeft ??= "[";
            BorderRight ??= "]";
            Size = ColumnDefinition.Fixed(BorderLeft.Length + "100%".Length + BorderRight.Length);
        }
    }

    public class BarStyleDefinition : ProgressStyleDefinition
    {
        public BarStyleDefinition(int size = 40)
        {
            BorderLeft = "[";
            BorderRight = "]";
            Size = ColumnDefinition.Fixed(size);
            FillChar = '=';
        }

        public char FillChar { get; set; }
    }


    public class SpinnerStyleDefinition : ProgressStyleDefinition
    {
        public SpinnerStyleDefinition()
        {
            Size = ColumnDefinition.SizeToContent();
            AnimationSymbols = DefaultAnimationSymbols;
        }

        internal static readonly char[] DefaultAnimationSymbols = new[] { '/', '-', '\\', '|' };

        public char[] AnimationSymbols { get; set; }
    }


    public interface IProgressVisualizer<in T>
    {
        public int GetTargetWidth();

        public TextSpan Visualize(T value, int renderWidth, TextSpanFormatter formatter);
    }

    public abstract class ProgressStyle : ProgressStyle<(int current, int total)>
    {
        public static ProgressStyle Bar(int size = 40, char fillChar = '=') => Bar(new BarStyleDefinition(size) { FillChar = fillChar });
        public static ProgressStyle Bar(BarStyleDefinition styleDefinition) => new ProgressBarStyle(styleDefinition);
        public static ProgressStyle Percentage(bool withBorder = true) => Percentage(new PercentStyleDefinition(border: withBorder ? null : string.Empty));
        public static ProgressStyle Percentage(PercentStyleDefinition styleDefinition) => new ProgressPercentTextStyle(styleDefinition);
        public static ProgressStyle Spinner(params char[] symbols) => Spinner(new SpinnerStyleDefinition() { AnimationSymbols = symbols ?? SpinnerStyleDefinition.DefaultAnimationSymbols });
        public static ProgressStyle Spinner(SpinnerStyleDefinition styleDefinition) => new ProgressSpinnerStyle(styleDefinition);


        protected static double CalculateRatio((int current, int total) value) => CalculateRatio(value.current, value.total);

        /// <inheritdoc />
        protected ProgressStyle(ProgressStyleDefinition styleDefinition) : base(styleDefinition) { }


        class ProgressBarStyle : ProgressStyle
        {
            private readonly char _fillChar;

            public ProgressBarStyle(BarStyleDefinition styleDefinition) : base(styleDefinition)
                => _fillChar = styleDefinition.FillChar;


            /// <inheritdoc />
            protected override int GetContentWidth() => 40;


            /// <inheritdoc />
            public override TextSpan Visualize((int current, int total) value, int renderWidth, TextSpanFormatter formatter)
            {
                var ratio = CalculateRatio(value);
                var barLength = (int)Math.Round(ratio * renderWidth);

                return formatter.ParseToSpan($"{string.Concat(Enumerable.Repeat(_fillChar, barLength))}");
            }
        }

        class ProgressPercentTextStyle : ProgressStyle
        {
            /// <inheritdoc />
            public ProgressPercentTextStyle(PercentStyleDefinition styleDefinition) : base(styleDefinition) { }


            /// <inheritdoc />
            protected override int GetContentWidth() => 4;


            /// <inheritdoc />
            public override TextSpan Visualize((int current, int total) value, int renderWidth, TextSpanFormatter formatter)
            {
                var ratio = CalculateRatio(value);
                var percentage = $"{ratio:P0}";
                var alignment = (renderWidth < percentage.Length)
                                    ? string.Empty
                                    : string.Concat(Enumerable.Repeat(" ", renderWidth - percentage.Length));
                return formatter.ParseToSpan($"{alignment}{percentage}");
            }
        }

        class ProgressSpinnerStyle : ProgressStyle
        {
            private readonly char[] _symbols;
            private int _index = 0;

            public ProgressSpinnerStyle(SpinnerStyleDefinition styleDefinition) : base(styleDefinition)
            {
                _symbols = styleDefinition.AnimationSymbols;
            }


            /// <inheritdoc />
            protected override int GetContentWidth() => 1;


            /// <inheritdoc />
            public override TextSpan Visualize((int current, int total) value, int renderWidth, TextSpanFormatter formatter)
            {
                var symbol = _symbols[_index];
                _index = (_index + 1) % _symbols.Length;
                var alignment = (renderWidth < 1)
                                    ? string.Empty
                                    : string.Concat(Enumerable.Repeat(" ", renderWidth - 1));
                return formatter.ParseToSpan($"{alignment}{symbol}");
            }
        }
    }

    public abstract class ProgressStyle<T> : IProgressVisualizer<T>
    {
        protected ProgressStyle(ProgressStyleDefinition styleDefinition) => StyleDefinition = styleDefinition;

        public ProgressStyleDefinition StyleDefinition { get; }

        protected static double CalculateRatio(double current, int total) => Math.Min(current / total, 1.0);

        protected abstract int GetContentWidth();

        public abstract TextSpan Visualize(T value, int renderWidth, TextSpanFormatter formatter);


        public int GetTargetWidth()
        {
            var sizeDefinition = StyleDefinition.Size;
            return sizeDefinition.SizeMode switch
            {
                SizeMode.Fixed => (int)sizeDefinition.Value,
                SizeMode.Star => int.MaxValue,
                SizeMode.SizeToContent => GetContentWidth(),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }


    public class ProgressView<T> : View
    {
        private bool _layoutInitialized;
        private StackLayoutView _layout { get; }
        private readonly View _content;
        private readonly ProgressStyleDefinition _styleDefinition;


        public ProgressView(View content, ProgressStyleDefinition styleDefinition)
        {
            _content = content;
            _styleDefinition = styleDefinition;
            _layout = new StackLayoutView(Orientation.Horizontal);
            _layout.Updated += OnLayoutUpdated;
        }

        private void OnLayoutUpdated(object sender, EventArgs e) => OnUpdated();

        /// <inheritdoc />
        public override Size Measure(ConsoleRenderer renderer, Size maxSize)
        {
            EnsureInitialized();
            var rv = _layout.Measure(renderer, maxSize);
            return rv;
        }

        /// <inheritdoc />
        public override void Render(ConsoleRenderer renderer, Region region)
        {
            EnsureInitialized();
            _layout.Render(renderer, region);
        }

        private void EnsureInitialized()
        {
            if (_layoutInitialized)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_styleDefinition.BorderLeft))
            {
                _layout.Add(new ContentView(_styleDefinition.BorderLeft));
            }
            _layout.Add(_content);
            if (!string.IsNullOrEmpty(_styleDefinition.BorderRight))
            {
                _layout.Add(new ContentView(_styleDefinition.BorderRight));
            }

            _layoutInitialized = true;
        }

        public static ProgressView<(int current, int total)> FromObservable(IObservable<(int current, int total)> observable, ProgressStyle<(int current, int total)> style)
            => ProgressView<(int current, int total)>.FromObservable(observable, converter: t => t, style);


        public static ProgressView<T> FromObservable<V>(IObservable<T> observable, Func<T, V> converter, ProgressStyle<V> style)
        {
            var adapterProvider = new ProgressVisualizerAdapter<T, V>(style, converter);
            var progressContentView = ProgressContentView<T>.FromObservable(observable, adapterProvider);
            var progressView = new ProgressView<T>(progressContentView, style.StyleDefinition);
            return progressView;
        }

        private class ProgressContentView<TItem> : View
        {
            private readonly IProgressVisualizer<TItem> _progressFormatter;

            public TItem Item { get; protected set; }

            /// <inheritdoc />
            public ProgressContentView(IProgressVisualizer<TItem> progressFormatter) => _progressFormatter = progressFormatter;


            /// <inheritdoc />
            public override void Render(ConsoleRenderer renderer, Region region)
            {
                if (renderer == null) throw new ArgumentNullException(nameof(renderer));
                if (region == null) throw new ArgumentNullException(nameof(region));

                var span = TextSpan.Empty();
                if (Item != null)
                {
                    var actualWidth = GetActualWidth(region);
                    span = _progressFormatter.Visualize(Item, actualWidth, renderer.Formatter);
                }

                renderer.RenderToRegion(span, region);
            }


            /// <inheritdoc />
            public override Size Measure(ConsoleRenderer renderer, Size maxSize)
            {
                if (renderer == null)
                {
                    throw new ArgumentNullException(nameof(renderer));
                }

                if (maxSize == null)
                {
                    throw new ArgumentNullException(nameof(maxSize));
                }

                var possibleWidth = GetActualWidth(maxSize);
                return new Size(possibleWidth, height: 1);
            }


            private int GetActualWidth(Region region) => GetActualWidth(new Size(region.Width, region.Height));


            private int GetActualWidth(Size maxSize) => Math.Min(_progressFormatter.GetTargetWidth(), maxSize.Width);


            protected void Observe(IObservable<TItem> observable)
            {
                if (observable == null)
                {
                    throw new ArgumentNullException(nameof(observable));
                }

                observable.Subscribe(new Observer(this));
            }


            public static ProgressContentView<TItem> FromObservable(IObservable<TItem> observable, IProgressVisualizer<TItem> progressFormatter)
            {
                var rv = new ProgressContentView<TItem>(progressFormatter);
                rv.Observe(observable);
                return rv;
            }

            private class Observer : IObserver<TItem>
            {
                private readonly ProgressContentView<TItem> _progressContentView;


                public Observer(ProgressContentView<TItem> progressContentView) => _progressContentView = progressContentView;


                public void OnCompleted() { /* TODO provide OnCompleted */ }

                public void OnError(Exception error) { /* TODO provide OnError */ }

                public void OnNext(TItem value)
                {
                    _progressContentView.Item = value;
                    _progressContentView.OnUpdated();
                }
            }
        }


        private class ProgressVisualizerAdapter<TSource, TResult> : IProgressVisualizer<TSource>
        {
            private readonly ProgressStyle<TResult> _targetStyle;

            private readonly Func<TSource, TResult> _converter;


            public ProgressVisualizerAdapter(ProgressStyle<TResult> targetStyle, Func<TSource, TResult> converter)
            {
                _targetStyle = targetStyle;
                _converter = converter;
            }


            /// <inheritdoc />
            public int GetTargetWidth() => _targetStyle.GetTargetWidth();


            /// <inheritdoc />
            public TextSpan Visualize(TSource value, int renderWidth, TextSpanFormatter formatter) => _targetStyle.Visualize(_converter(value), renderWidth, formatter);
        }
    }
}
