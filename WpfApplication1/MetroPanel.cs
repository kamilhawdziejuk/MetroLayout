using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace WpfApplication1
{
    public class MetroPanel : WrapPanel
    {
        private struct UVSize
        {
            internal UVSize(Orientation orientation, double width, double height)
            {
                U = V = 0d;
                _orientation = orientation;
                Width = width;
                Height = height;
            }

            internal UVSize(Orientation orientation)
            {
                U = V = 0d;
                _orientation = orientation;
            }

            internal double U;
            internal double V;
            private Orientation _orientation;

            internal double Width
            {
                get { return (_orientation == Orientation.Horizontal ? U : V); }
                set { if (_orientation == Orientation.Horizontal) U = value; else V = value; }
            }
            internal double Height
            {
                get { return (_orientation == Orientation.Horizontal ? V : U); }
                set { if (_orientation == Orientation.Horizontal) V = value; else U = value; }
            }

            public override string ToString()
            {
                return "U: " + U + " V: " + V;
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            int firstInLine = 0;
            double itemWidth = ItemWidth;
            double itemHeight = ItemHeight;
            double accumulatedV = 0;
            double itemU = (Orientation == Orientation.Horizontal ? itemWidth : itemHeight);
            UVSize curLineSize = new UVSize(Orientation);
            UVSize uvFinalSize = new UVSize(Orientation, finalSize.Width, finalSize.Height);
            bool itemWidthSet = !double.IsNaN(itemWidth);
            bool itemHeightSet = !double.IsNaN(itemHeight);
            bool useItemU = (Orientation == Orientation.Horizontal ? itemWidthSet : itemHeightSet);
            UVSize prev = new UVSize();
            UIElementCollection children = InternalChildren;
            double accV = 0;
            for (int i = 0, count = children.Count; i < count; i++)
            {
                UIElement child = children[i] as UIElement;
                if (child == null) continue;

                UVSize sz = new UVSize(
                    Orientation,
                    (itemWidthSet ? itemWidth : child.DesiredSize.Width),
                    (itemHeightSet ? itemHeight : child.DesiredSize.Height));

                if (NeedsAnotherLine(curLineSize, uvFinalSize, sz, prev, accV)) //need to switch to another line
                {
                    arrangeLine(accumulatedV, curLineSize.V, firstInLine, i, useItemU, itemU);

                    accumulatedV += curLineSize.V;
                    curLineSize = sz;

                    if (sz.U > uvFinalSize.U) //the element is wider then the constraint - give it a separate line                    
                    {
                        //switch to next line which only contain one element
                        arrangeLine(accumulatedV, sz.V, i, ++i, useItemU, itemU);

                        accumulatedV += sz.V;
                        curLineSize = new UVSize(Orientation);
                    }
                    firstInLine = i;
                }
                else //continue to accumulate a line
                {
                    if (prev.V + sz.V > curLineSize.V)
                    {
                        // dock on the right
                        curLineSize.U += sz.U;
                        curLineSize.V = Math.Max(sz.V, curLineSize.V);
                        accV = sz.V;
                    }
                    else
                    {
                        // dock at the bottom
                        curLineSize.U += Math.Max(sz.U - prev.U, 0);
                        curLineSize.V = Math.Max(sz.V + prev.V, curLineSize.V);
                        accV += sz.V;
                    }
                }
                prev = sz;
            }

            //arrange the last line, if any
            if (firstInLine < children.Count)
            {
                arrangeLine(accumulatedV, curLineSize.V, firstInLine, children.Count, useItemU, itemU);
            }

            return finalSize;
        }

        /// <summary>
        /// <see cref="FrameworkElement.MeasureOverride"/>
        /// </summary>
        protected override Size MeasureOverride(Size constraint)
        {
            Debug.WriteLine("constraining size: " + constraint);
            UVSize curLineSize = new UVSize(Orientation);
            UVSize panelSize = new UVSize(Orientation);
            UVSize uvConstraint = new UVSize(Orientation, constraint.Width, constraint.Height);
            double accV = 0;
            int firstInLine = 0;
            double itemWidth = ItemWidth;
            double itemHeight = ItemHeight;
            bool itemWidthSet = !double.IsNaN(itemWidth);
            bool itemHeightSet = !double.IsNaN(itemHeight);

            Size childConstraint = new Size(
                (itemWidthSet ? itemWidth : constraint.Width),
                (itemHeightSet ? itemHeight : constraint.Height));
            UVSize prev = new UVSize();
            UIElementCollection children = InternalChildren;

            for (int i = 0, count = children.Count; i < count; i++)
            {
                UIElement child = children[i] as UIElement;
                if (child == null) continue;

                //Flow passes its own constrint to children
                child.Measure(childConstraint);

                //this is the size of the child in UV space
                UVSize sz = new UVSize(
                    Orientation,
                    (itemWidthSet ? itemWidth : child.DesiredSize.Width),
                    (itemHeightSet ? itemHeight : child.DesiredSize.Height));

                if (NeedsAnotherLine(curLineSize, uvConstraint, sz, prev, accV)) //need to switch to another line
                {
                    panelSize.U = Math.Max(curLineSize.U, panelSize.U);
                    panelSize.V += curLineSize.V;
                    curLineSize = sz;

                    if (sz.U > uvConstraint.U) //the element is wider then the constrint - give it a separate line                    
                    {
                        panelSize.U = Math.Max(sz.U, panelSize.U);
                        panelSize.V += sz.V;
                        curLineSize = new UVSize(Orientation);
                    }
                    firstInLine = i;
                }
                else //continue to accumulate a line
                {
                    if (accV + sz.V > curLineSize.V)
                    {
                        // dock on the right
                        curLineSize.U += sz.U;
                        curLineSize.V = Math.Max(sz.V, curLineSize.V);
                        accV = sz.V;
                    }
                    else
                    {
                        // dock at the bottom
                        curLineSize.U += Math.Max(sz.U - prev.U, 0);
                        curLineSize.V = Math.Max(sz.V + prev.V, curLineSize.V);
                        accV += sz.V;
                    }
                }
                prev = sz;
            }

            //the last line size, if any should be added
            panelSize.U = Math.Max(curLineSize.U, panelSize.U);
            panelSize.V += curLineSize.V;

            //go from UV space to W/H space
            Debug.WriteLine("panel size" + panelSize);
            return new Size(panelSize.Width, panelSize.Height);
        }

        private static bool NeedsAnotherLine(UVSize curLineSize, UVSize uvFinalSize, UVSize sz, UVSize prev, double heightSoFar)
        {
            if (heightSoFar + sz.V > curLineSize.V)
            {
                // dock on the right
                return curLineSize.U + sz.U > uvFinalSize.U;
            }
            else
            {
                // dock at the bottom
                return curLineSize.U - prev.U + sz.U > uvFinalSize.U;

            }
        }

        private void arrangeLine(double v, double lineV, int start, int end, bool useItemU, double itemU)
        {
            double accV = 0;
            var origV = v;
            double u = 0;
            bool isHorizontal = (Orientation == Orientation.Horizontal);

            UIElementCollection children = InternalChildren;
            UVSize prev = new UVSize();
            for (int i = start; i < end; i++)
            {
                UIElement child = children[i] as UIElement;
                if (child != null)
                {
                    UVSize childSize = new UVSize(Orientation, child.DesiredSize.Width, child.DesiredSize.Height);
                    double layoutSlotU = (useItemU ? itemU : childSize.U);

                    if (i == start || accV + childSize.V > lineV)
                    {
                        v = origV;
                        // dock on the right
                        var r = new Rect(
                       (isHorizontal ? u : v),
                       (isHorizontal ? v : u),
                       (isHorizontal ? layoutSlotU : lineV),
                       (isHorizontal ? childSize.V : layoutSlotU));
                        Debug.WriteLine(r);

                        child.Arrange(r);
                        u += layoutSlotU;
                        accV = childSize.V;
                    }
                    else
                    {
                        v += prev.V;
                        // dock on the bottom
                        var r = new Rect(u - prev.U, v, layoutSlotU, childSize.V);
                        Debug.WriteLine(r);
                        child.Arrange(r);
                        u += Math.Max(0, layoutSlotU - prev.U);
                        accV += childSize.V;
                    }
                    prev = childSize;
                }
            }
        }
    }

}
