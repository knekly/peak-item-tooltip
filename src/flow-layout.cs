/**
 * @authors @katcw
 * @file    flow-layout.cs
 * @brief   a ugui layout group that places children left to right and wraps them onto new lines
 */
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PeakItemTooltip
{
    /**
     * @brief lays children out in reading order at their preferred sizes, wrapping to a new line when the next
     *        child would pass the right edge. width is left to the parent; height fits the wrapped lines.
     *        children on a line are centred vertically on that line
     */
    public class FlowLayoutGroup : LayoutGroup
    {
        public float spacingX = 12f;
        public float spacingY = 2f;
        // when set, every child takes a full-width line of its own (a plain vertical list). unity allows only
        // one layout group per object, so this one switches modes rather than swapping groups
        public bool oneItemPerLine;

        // per-child position on its line, filled by Wrap and reused for both axes
        private readonly List<Vector2> _positions = new List<Vector2>();
        private readonly List<Vector2> _sizes = new List<Vector2>();
        private float _contentHeight;

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            // no preferred width of our own: the parent decides how wide the block is and lines wrap to fit
            SetLayoutInputForAxis(padding.horizontal, padding.horizontal, -1f, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            Wrap();
            float h = _contentHeight + padding.vertical;
            SetLayoutInputForAxis(h, h, -1f, 1);
        }

        public override void SetLayoutHorizontal()
        {
            Wrap();
            for (int i = 0; i < rectChildren.Count; i++)
                SetChildAlongAxis(rectChildren[i], 0, padding.left + _positions[i].x, _sizes[i].x);
        }

        public override void SetLayoutVertical()
        {
            Wrap();
            for (int i = 0; i < rectChildren.Count; i++)
                SetChildAlongAxis(rectChildren[i], 1, padding.top + _positions[i].y, _sizes[i].y);
        }

        /**
         * @brief breaks the children into lines for the current width and records each one's position and size
         */
        private void Wrap()
        {
            _positions.Clear();
            _sizes.Clear();

            float available = Mathf.Max(0f, rectTransform.rect.width - padding.horizontal);
            float x = 0f;
            float lineTop = 0f;
            float lineHeight = 0f;
            int lineStart = 0;

            for (int i = 0; i < rectChildren.Count; i++)
            {
                RectTransform child = rectChildren[i];
                // a child wider than the block is capped to it (its text then wraps inside)
                float w = oneItemPerLine ? available : Mathf.Min(LayoutUtility.GetPreferredWidth(child), available);
                float h = LayoutUtility.GetPreferredHeight(child);

                if (i > lineStart && (oneItemPerLine || x + w > available))
                {
                    CentreLine(lineStart, i, lineHeight);
                    lineTop += lineHeight + spacingY;
                    x = 0f;
                    lineHeight = 0f;
                    lineStart = i;
                }

                _positions.Add(new Vector2(x, lineTop));
                _sizes.Add(new Vector2(w, h));
                x += w + spacingX;
                lineHeight = Mathf.Max(lineHeight, h);
            }

            CentreLine(lineStart, rectChildren.Count, lineHeight);
            _contentHeight = rectChildren.Count > 0 ? lineTop + lineHeight : 0f;
        }

        /**
         * @brief shifts the children of one line down so each is centred on the line's height
         */
        private void CentreLine(int start, int end, float lineHeight)
        {
            for (int i = start; i < end; i++)
                _positions[i] = new Vector2(_positions[i].x, _positions[i].y + (lineHeight - _sizes[i].y) * 0.5f);
        }
    }
}
