using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace RenoDXCommander.Controls;

public class WrapPanel : Panel
{
    public double HorizontalSpacing { get; set; } = 8;
    public double VerticalSpacing { get; set; } = 4;

    protected override Size MeasureOverride(Size availableSize)
    {
        double x = 0, rowHeight = 0, totalHeight = 0, maxWidth = 0;
        foreach (UIElement child in Children)
        {
            if (child.Visibility == Visibility.Collapsed) continue;
            child.Measure(availableSize);
            var desired = child.DesiredSize;
            if (x > 0 && x + desired.Width > availableSize.Width)
            {
                maxWidth = Math.Max(maxWidth, x - HorizontalSpacing);
                totalHeight += rowHeight + VerticalSpacing;
                x = 0; rowHeight = 0;
            }
            x += desired.Width + HorizontalSpacing;
            rowHeight = Math.Max(rowHeight, desired.Height);
        }
        maxWidth = Math.Max(maxWidth, x > 0 ? x - HorizontalSpacing : 0);
        totalHeight += rowHeight;
        return new Size(maxWidth, totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // First pass: compute row heights
        var rowStarts = new List<(int start, double rowHeight)>();
        double x = 0, rowHeight = 0;
        int rowStart = 0, idx = 0;
        var visible = new List<(UIElement el, int index)>();
        foreach (UIElement child in Children)
        {
            if (child.Visibility == Visibility.Collapsed) continue;
            var desired = child.DesiredSize;
            if (x > 0 && x + desired.Width > finalSize.Width)
            {
                rowStarts.Add((rowStart, rowHeight));
                rowStart = visible.Count;
                x = 0; rowHeight = 0;
            }
            visible.Add((child, visible.Count));
            x += desired.Width + HorizontalSpacing;
            rowHeight = Math.Max(rowHeight, desired.Height);
        }
        if (visible.Count > rowStart)
            rowStarts.Add((rowStart, rowHeight));

        // Second pass: arrange with row height for stretch
        x = 0; double y = 0; int rowIdx = 0;
        int nextRowStart = rowStarts.Count > 1 ? rowStarts[1].start : int.MaxValue;
        double currentRowHeight = rowStarts.Count > 0 ? rowStarts[0].rowHeight : 0;
        for (int i = 0; i < visible.Count; i++)
        {
            if (i >= nextRowStart)
            {
                rowIdx++;
                y += currentRowHeight + VerticalSpacing;
                x = 0;
                currentRowHeight = rowStarts[rowIdx].rowHeight;
                nextRowStart = rowIdx + 1 < rowStarts.Count ? rowStarts[rowIdx + 1].start : int.MaxValue;
            }
            var child = visible[i].el;
            var desired = child.DesiredSize;
            if (x > 0 && x + desired.Width > finalSize.Width)
            {
                // shouldn't happen since we pre-computed, but safety
                y += currentRowHeight + VerticalSpacing;
                x = 0;
            }
            var h = (child is FrameworkElement fe && fe.VerticalAlignment == VerticalAlignment.Stretch)
                ? currentRowHeight : desired.Height;
            var arrangeY = (child is FrameworkElement fe2 && fe2.VerticalAlignment == VerticalAlignment.Stretch)
                ? y : y + (currentRowHeight - desired.Height) / 2;
            child.Arrange(new Rect(x, arrangeY, desired.Width, h));
            x += desired.Width + HorizontalSpacing;
        }
        return finalSize;
    }
}
