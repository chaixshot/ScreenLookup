using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ScreenLookup.src.models;

namespace ScreenLookup.src.controls
{
    public class WordWrapPanel : Panel
    {
        public static readonly DependencyProperty WordSpacingProperty =
            DependencyProperty.Register(nameof(WordSpacing), typeof(double), typeof(WordWrapPanel),
                new FrameworkPropertyMetadata(3.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        public double WordSpacing
        {
            get => (double)GetValue(WordSpacingProperty);
            set => SetValue(WordSpacingProperty, value);
        }

        public static readonly DependencyProperty LineSpacingProperty =
            DependencyProperty.Register(nameof(LineSpacing), typeof(double), typeof(WordWrapPanel),
                new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        public double LineSpacing
        {
            get => (double)GetValue(LineSpacingProperty);
            set => SetValue(LineSpacingProperty, value);
        }

        public static readonly DependencyProperty ParagraphSpacingProperty =
            DependencyProperty.Register(nameof(ParagraphSpacing), typeof(double), typeof(WordWrapPanel),
                new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        public double ParagraphSpacing
        {
            get => (double)GetValue(ParagraphSpacingProperty);
            set => SetValue(ParagraphSpacingProperty, value);
        }

        private static (bool isBreak, int stop) GetBreakInfo(UIElement child)
        {
            if (child is FrameworkElement fe && fe.DataContext is CaptureWordsEntry cwe)
            {
                if (cwe.Stop > 0 || string.IsNullOrEmpty(cwe.Word))
                {
                    return (true, cwe.Stop > 0 ? cwe.Stop : 1);
                }
            }
            return (false, 0);
        }

        private static bool HasRemainingWords(UIElementCollection children, int startIndex)
        {
            for (int i = startIndex; i < children.Count; i++)
            {
                UIElement child = children[i];
                if (child.Visibility == Visibility.Collapsed)
                    continue;

                var (isBreak, _) = GetBreakInfo(child);
                if (!isBreak)
                    return true;
            }
            return false;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double curLineX = 0;
            double curLineY = 0;
            double curLineHeight = 0;
            double maxLineWidth = 0;
            double defaultLineHeight = 24.0;

            for (int i = 0; i < InternalChildren.Count; i++)
            {
                UIElement child = InternalChildren[i];
                var (isBreak, stop) = GetBreakInfo(child);

                if (isBreak)
                {
                    child.Measure(new Size(0, 0));

                    if (HasRemainingWords(InternalChildren, i + 1))
                    {
                        double lineH = curLineHeight > 0 ? curLineHeight : defaultLineHeight;
                        curLineY += lineH + (stop >= 2 ? ParagraphSpacing : LineSpacing);
                        maxLineWidth = Math.Max(maxLineWidth, curLineX);
                        curLineX = 0;
                        curLineHeight = 0;
                    }
                    continue;
                }

                if (child.Visibility == Visibility.Collapsed)
                    continue;

                child.Measure(availableSize);
                Size childSize = child.DesiredSize;

                if (childSize.Width == 0 && childSize.Height == 0)
                    continue;

                if (childSize.Height > 0)
                    defaultLineHeight = childSize.Height;

                // Soft wrap if exceeding width constraint
                if (!double.IsInfinity(availableSize.Width) && curLineX + childSize.Width > availableSize.Width && curLineX > 0)
                {
                    double lineH = curLineHeight > 0 ? curLineHeight : defaultLineHeight;
                    curLineY += lineH + LineSpacing;
                    maxLineWidth = Math.Max(maxLineWidth, curLineX);
                    curLineX = 0;
                    curLineHeight = 0;
                }

                curLineX += childSize.Width + WordSpacing;
                curLineHeight = Math.Max(curLineHeight, childSize.Height);
            }

            maxLineWidth = Math.Max(maxLineWidth, curLineX);
            curLineY += curLineHeight;

            return new Size(maxLineWidth, curLineY);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            double curLineX = 0;
            double curLineY = 0;
            double curLineHeight = 0;
            double defaultLineHeight = 24.0;

            var lineChildren = new List<(UIElement element, double x, double width, double height)>();

            void FlushLine(bool isBreak, int stop, bool hasRemaining)
            {
                double lineH = curLineHeight > 0 ? curLineHeight : defaultLineHeight;
                foreach (var item in lineChildren)
                {
                    item.element.Arrange(new Rect(item.x, curLineY, item.width, item.height));
                }
                lineChildren.Clear();

                if (isBreak)
                {
                    if (hasRemaining)
                    {
                        curLineY += lineH + (stop >= 2 ? ParagraphSpacing : LineSpacing);
                    }
                    else
                    {
                        curLineY += lineH;
                    }
                }
                else
                {
                    curLineY += lineH + LineSpacing;
                }

                curLineX = 0;
                curLineHeight = 0;
            }

            for (int i = 0; i < InternalChildren.Count; i++)
            {
                UIElement child = InternalChildren[i];
                var (isBreak, stop) = GetBreakInfo(child);

                if (isBreak)
                {
                    child.Arrange(new Rect(0, 0, 0, 0));
                    bool hasRemaining = HasRemainingWords(InternalChildren, i + 1);
                    FlushLine(true, stop, hasRemaining);
                    continue;
                }

                if (child.Visibility == Visibility.Collapsed)
                    continue;

                Size childSize = child.DesiredSize;
                if (childSize.Width == 0 && childSize.Height == 0)
                {
                    child.Arrange(new Rect(0, 0, 0, 0));
                    continue;
                }

                if (childSize.Height > 0)
                    defaultLineHeight = childSize.Height;

                // Soft wrap
                if (!double.IsInfinity(finalSize.Width) && curLineX + childSize.Width > finalSize.Width && curLineX > 0)
                {
                    FlushLine(false, 0, true);
                }

                lineChildren.Add((child, curLineX, childSize.Width, childSize.Height));
                curLineX += childSize.Width + WordSpacing;
                curLineHeight = Math.Max(curLineHeight, childSize.Height);
            }

            if (lineChildren.Count > 0)
            {
                FlushLine(false, 0, false);
            }

            return finalSize;
        }
    }
}
