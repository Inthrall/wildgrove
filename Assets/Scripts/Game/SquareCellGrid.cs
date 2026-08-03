using UnityEngine;
using UnityEngine.UI;

namespace Wildgrove.Game
{
    /// <summary>
    /// A grid of square cells that fills the width it is given. uGUI's
    /// <see cref="GridLayoutGroup"/> takes a fixed cell size and a fixed column
    /// count, which is the wrong shape for the journal: the same card is a
    /// ~1030-unit phone column and a ~470-unit page of a spread, and a cell
    /// size that suits one leaves a ragged margin on the other.
    /// <para>
    /// So the column count is chosen per width — whichever count between
    /// <see cref="minColumns"/> and <see cref="maxColumns"/> lands a cell
    /// closest to <see cref="idealCell"/> — and the cell is then sized to
    /// divide the width exactly, gutters included. Square because the grid
    /// draws plates, and a drawer of specimens reads as a drawer only when
    /// every drawer is the same shape.
    /// </para>
    /// <para>
    /// The fit runs in <see cref="SetLayoutHorizontal"/>, not in the calculate
    /// pass, because that is the first point at which the rect carries the
    /// width its parent actually gave it. uGUI runs the two vertical passes
    /// after both horizontal ones, so the row count and the grid's height are
    /// measured from the cell size settled here — one pass, no rebuild loop.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Public, like <see cref="JournalLayout"/> and <see cref="JournalNav"/>,
    /// because the EditMode tests live in their own assembly and the fit is
    /// maths worth pinning.
    /// </remarks>
    public sealed class SquareCellGrid : GridLayoutGroup
    {
        /// <summary>The cell size the grid aims for. Column counts are judged by how near they land to it.</summary>
        public float idealCell = 190f;

        public int minColumns = 3;
        public int maxColumns = 5;

        public override void SetLayoutHorizontal()
        {
            Fit(rectTransform.rect.width);
            base.SetLayoutHorizontal();
        }

        private void Fit(float width)
        {
            var columns = ColumnsFor(width, idealCell, padding.horizontal, spacing.x, minColumns, maxColumns);
            var cell = CellFor(width, columns, padding.horizontal, spacing.x);
            constraint = Constraint.FixedColumnCount;
            constraintCount = columns;
            cellSize = new Vector2(cell, cell);
        }

        /// <summary>
        /// The square cell that divides <paramref name="width"/> into
        /// <paramref name="columns"/> once the padding and the gutters between
        /// them are taken out. Never negative — a width narrower than its own
        /// gutters gives a zero cell rather than an inside-out one.
        /// </summary>
        public static float CellFor(float width, int columns, float padding, float spacing)
        {
            if (columns <= 0)
            {
                return 0f;
            }

            return Mathf.Max(0f, (width - padding - spacing * (columns - 1)) / columns);
        }

        /// <summary>
        /// The column count whose cell lands nearest the ideal. Ties go to the
        /// smaller count (bigger cells), which is the kinder miss on a phone:
        /// an extra column costs a plate more than a wide margin does.
        /// </summary>
        public static int ColumnsFor(float width, float idealCell, float padding, float spacing,
            int minColumns, int maxColumns)
        {
            minColumns = Mathf.Max(1, minColumns);
            maxColumns = Mathf.Max(minColumns, maxColumns);

            var best = minColumns;
            var bestMiss = Mathf.Abs(CellFor(width, best, padding, spacing) - idealCell);
            for (var columns = minColumns + 1; columns <= maxColumns; columns++)
            {
                var miss = Mathf.Abs(CellFor(width, columns, padding, spacing) - idealCell);
                if (miss < bestMiss)
                {
                    best = columns;
                    bestMiss = miss;
                }
            }

            return best;
        }
    }
}
