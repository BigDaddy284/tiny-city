using System;

namespace TinyCity.Art
{
    /// <summary>
    /// Draws the residents: a figure about 33 pixels tall (doors are about 30), in three-quarter view facing one of
    /// the four walking directions, with the same light, outline and shadow as everything else. The anchor is the
    /// point the figure stands on, or the seat for someone sitting; someone lying down is drawn on the bed's tile.
    /// </summary>
    public static class ResidentArt
    {
        public const int Width = 32;
        public const int Height = 56;
        public const int AnchorX = 16;
        public const int AnchorY = 44;

        /// <summary>How far above the anchor the top of the head is, for the mood icon.</summary>
        public static int HeadTop(ResidentLook look, ResidentPose pose)
        {
            var extra = look.Hat != null ? 3 : look.Style == HairStyle.Bun ? 2 : 0;
            if (look.IsBaby)
            {
                return BabyHeight;
            }

            switch (pose.Stance)
            {
                case Stance.Sitting: return 22 + extra - (look.Shrink / 2);
                case Stance.Lying: return 22;
                default: return 32 + extra - look.Shrink;
            }
        }

        /// <summary>How far above the anchor a carried baby's head is: about chest height on whoever carries them.</summary>
        public const int BabyHeight = 24;

        public static PixelCanvas Render(ResidentLook look, ResidentPose pose)
        {
            var canvas = new PixelCanvas(Width, Height, AnchorX, AnchorY);
            if (look.IsBaby)
            {
                Baby(canvas, look, pose.Facing.Away, pose.Stance == Stance.Lying);
                canvas.Outline(Palette.Outline);
                return canvas;
            }

            if (pose.Stance == Stance.Lying)
            {
                Lying(canvas, look);
                canvas.Outline(Palette.Outline);
                return canvas;
            }

            var f = new Figure(canvas, look, pose);
            f.Draw();
            canvas.Outline(Palette.Outline);
            if (look.Shrink > 0)
            {
                // A child: the same figure with rows taken out between the neck and the ankles, so the head stays its
                // size and the feet stay on the ground (or, sitting, on the seat).
                var seated = pose.Stance == Stance.Sitting;
                Shorten(canvas, seated ? look.Shrink / 2 : look.Shrink, AnchorY - (seated ? 11 : 21), AnchorY - (seated ? 1 : 4));
            }

            if (pose.Stance == Stance.Standing)
            {
                canvas.Ellipse(AnchorX, AnchorY - 0.5, look.Shrink > 0 ? 5 : 7, look.Shrink > 0 ? 2 : 2.4, Palette.Shadow, under: true);
            }

            return canvas;
        }

        /// <summary>Takes <paramref name="rows"/> evenly spaced rows out of [top, bottom], moving everything above down to close the gap.</summary>
        private static void Shorten(PixelCanvas canvas, int rows, int top, int bottom)
        {
            var span = bottom - top + 1;
            rows = Math.Max(0, Math.Min(rows, span - 1));
            var removed = new bool[canvas.Height];
            for (var k = 0; k < rows; k++)
            {
                removed[top + (int)((k + 0.5) * span / rows)] = true;
            }

            var target = bottom;
            for (var y = bottom; y >= 0; y--)
            {
                if (y >= top && removed[y])
                {
                    continue;
                }

                for (var x = 0; x < canvas.Width; x++)
                {
                    canvas.Set(x, target, canvas.Get(x, y));
                }

                target--;
            }

            for (; target >= 0; target--)
            {
                for (var x = 0; x < canvas.Width; x++)
                {
                    canvas.Set(x, target, Rgba.Clear);
                }
            }
        }

        /// <summary>
        /// A baby wrapped in a blanket, held at chest height a little to the side of whoever carries them, or tucked
        /// up low beside them when they're lying down.
        /// </summary>
        private static void Baby(PixelCanvas canvas, ResidentLook look, bool away, bool tuckedUp)
        {
            var side = tuckedUp ? 7 : away ? -4 : 4;
            var cx = AnchorX + side;
            var cy = AnchorY - (tuckedUp ? 8 : 17);
            canvas.Ellipse(cx, cy, 4.2, 2.6, look.Top);
            if (look.Trim != null)
            {
                canvas.Set(cx - 1, cy + 1, look.Trim.Value);
                canvas.Set(cx, cy + 1, look.Trim.Value);
                canvas.Set(cx + 1, cy + 1, look.Trim.Value);
            }

            var hx = cx + (away ? 2.5 : -2.5);
            var hy = cy - 3.0;
            canvas.Disc(hx, hy, 2.4, look.Skin);
            canvas.Disc(hx, hy - 1.6, 1.3, look.Hair);
            if (!away)
            {
                canvas.Set((int)Math.Round(hx - 1), (int)Math.Round(hy), Palette.Ebony);
                canvas.Set((int)Math.Round(hx + 1), (int)Math.Round(hy), Palette.Ebony);
            }
        }

        /// <summary>Tucked up in bed: the head on the pillow and the shoulders above the quilt (the bed draws itself).</summary>
        private static void Lying(PixelCanvas canvas, ResidentLook look)
        {
            var p = new IsoPainter(canvas);
            var (sx, sy) = p.Project(0.47, 0.5, 16);
            canvas.Ellipse(sx, sy, 4.5, 2.5, look.Top);
            var (hx, hy) = p.Project(0.3, 0.5, 17);
            canvas.Disc(hx, hy, 3.4, look.Skin);
            var hair = look.Style == HairStyle.Bald ? look.Skin : look.Hair;
            canvas.Disc(hx - 1.6, hy - 1.4, 2.6, hair);

            // Closed eyes.
            var ex = (int)Math.Round(hx + 0.5);
            var ey = (int)Math.Round(hy + 0.5);
            canvas.Set(ex, ey, Palette.Ebony);
            canvas.Set(ex + 2, ey - 1, Palette.Ebony);
        }

        /// <summary>
        /// One standing or sitting figure. Columns are counted from the centre (-6 to 5) as if facing right, and
        /// mirrored for facing left; rows are counted up from the anchor (negative is higher). Light always comes from
        /// the upper left, so the right-hand side is shaded whichever way the figure faces.
        /// </summary>
        private sealed class Figure
        {
            private readonly PixelCanvas canvas;
            private readonly ResidentLook look;
            private readonly ResidentPose pose;
            private readonly bool away;
            private readonly bool seated;
            private readonly int shift;

            public Figure(PixelCanvas canvas, ResidentLook look, ResidentPose pose)
            {
                this.canvas = canvas;
                this.look = look;
                this.pose = pose;
                away = pose.Facing.Away;
                seated = pose.Stance == Stance.Sitting;

                // Rows are drawn for a standing figure with the hips 11 pixels up; sitting, the hips are on the seat
                // (the anchor), so everything above them moves down 10, and walking or jumping bobs it up.
                shift = seated ? 10 : Bob();
            }

            public void Draw()
            {
                Legs();
                if (look.Style == HairStyle.Long || look.Style == HairStyle.Ponytail)
                {
                    HairBehind();
                }

                Torso();
                Head();
                Arms();
            }

            /// <summary>Walking and jumping bob the body up a pixel on alternate frames.</summary>
            private int Bob()
            {
                switch (pose.Action)
                {
                    case ResidentAction.Walk: return pose.Frame % 2 == 1 ? -1 : 0;
                    case ResidentAction.Exercise: return pose.Frame == 0 ? -2 : 0;
                    default: return 0;
                }
            }

            /// <summary>A row of the body above the legs.</summary>
            private int Row(int standingRow) => standingRow + shift;

            private void Put(int col, int row, Rgba colour)
            {
                var x = AnchorX + (pose.Facing.Left ? -1 - col : col);
                if (colour.IsClear)
                {
                    canvas.Set(x, AnchorY + row, colour);
                    return;
                }

                var shade = x >= AnchorX + 3 ? 0.86 : x <= AnchorX - 5 ? 1.04 : 1.0;
                canvas.Set(x, AnchorY + row, colour.Shade(shade));
            }

            private void Rect(int col0, int row0, int col1, int row1, Rgba colour)
            {
                for (var r = row0; r <= row1; r++)
                {
                    for (var c = col0; c <= col1; c++)
                    {
                        Put(c, r, colour);
                    }
                }
            }

            private void Legs()
            {
                var shoe = look.Bottom.Equals(Palette.Charcoal) ? Palette.WoodDark : Palette.Charcoal;
                if (seated)
                {
                    if (away)
                    {
                        return;
                    }

                    // Thighs forward along the seat, two shins hanging down towards the floor.
                    var shin = look.Skirt ? look.Skin : look.Bottom;
                    Rect(-3, -2, 5, 0, look.Bottom);
                    Rect(2, 1, 5, 7, shin);
                    Rect(4, 1, 4, 7, shin.Shade(0.8));
                    Rect(2, 8, 6, 9, shoe);
                    return;
                }

                // Two legs, a stride apart on walking frames 0 and 2, and wide on the jump of a star jump.
                var walk = pose.Action == ResidentAction.Walk ? (pose.Frame == 0 ? 1 : pose.Frame == 2 ? -1 : 0) : 0;
                var jump = pose.Action == ResidentAction.Exercise && pose.Frame == 0 ? 1 : 0;
                var back = walk != 0 ? -walk : -jump;
                var front = walk != 0 ? walk : jump;
                // Between strides one foot is lifted as it passes the other: the back one, then the front one.
                var liftBack = pose.Action == ResidentAction.Walk && pose.Frame == 1 ? 1 : 0;
                var liftFront = pose.Action == ResidentAction.Walk && pose.Frame == 3 ? 1 : 0;
                var leg = look.Skirt ? look.Skin : look.Bottom;
                Rect(-3 + back, Row(-10), -1 + back, -3 - liftBack, leg);
                Rect(0 + front, Row(-10), 2 + front, -3 - liftFront, leg);
                Rect(-3 + back, -2 - liftBack, -1 + back, -1 - liftBack, shoe);
                Rect(0 + front, -2 - liftFront, 2 + front, -1 - liftFront, shoe);
                if (!away)
                {
                    // Toes point the way they face, lit with the trim on techwear trainers.
                    Put(3 + front, -1 - liftFront, look.Trim ?? shoe);
                }
            }

            private void Torso()
            {
                if (look.Skirt)
                {
                    var hem = seated ? Row(-11) : Row(-6);
                    Rect(-4, Row(-12), 3, hem, look.Bottom);
                    Rect(-5, hem - 1, 4, hem, look.Bottom);
                    if (seated && !away)
                    {
                        Rect(-3, -1, 4, 0, look.Bottom);
                    }
                }
                else
                {
                    Rect(-3, Row(-12), 2, Row(-10), look.Bottom);
                }

                if (look.Coat && !seated)
                {
                    // A long coat hangs to the knees behind the legs and, open, down the front.
                    Rect(-4, Row(-12), -2, Row(-6), look.Top);
                    Rect(2, Row(-12), 3, Row(-7), look.Top);
                    if (look.Trim is Rgba hem)
                    {
                        Rect(-4, Row(-6), -2, Row(-6), hem);
                        Rect(2, Row(-7), 2, Row(-7), hem);
                    }
                }

                // The body, with rounded shoulders.
                Rect(-4, Row(-20), 3, Row(-12), look.Top);
                Put(-4, Row(-20), Rgba.Clear);
                Put(3, Row(-20), Rgba.Clear);
                if (!look.Skirt)
                {
                    // A belt between top and trousers, glowing on techwear.
                    Rect(-4, Row(-12), 3, Row(-12), look.Trim ?? look.Top.Shade(0.8));
                }
                else if (look.Trim is Rgba skirtHem)
                {
                    var hem = seated ? Row(-11) : Row(-6);
                    Rect(-5, hem, 4, hem, skirtHem);
                }

                if (look.Trim is Rgba trim)
                {
                    // The collar, and down the front a seam (or a stripe across the shoulders from behind).
                    Rect(-2, Row(-20), 1, Row(-20), trim);
                    if (away)
                    {
                        Rect(-3, Row(-17), 2, Row(-17), trim);
                    }
                    else
                    {
                        Rect(1, Row(-19), 1, Row(-13), trim);
                    }
                }

                if (look.Apron is Rgba apron && !away)
                {
                    Rect(-2, Row(-17), 3, Row(seated ? -10 : -7), apron);
                    Put(-1, Row(-18), apron);
                    Put(2, Row(-18), apron);
                    if (look.Trim is Rgba edge)
                    {
                        Rect(-2, Row(seated ? -10 : -7), 3, Row(seated ? -10 : -7), edge);
                    }
                }

                // Neck.
                Rect(-1, Row(-21), 1, Row(-21), look.Skin);
            }

            private void Head()
            {
                var hair = look.Hair;
                var skin = look.Skin;

                // Face (or the back of the head) with rounded lower corners.
                Rect(-4, Row(-28), 3, Row(-22), skin);
                Put(-4, Row(-22), Rgba.Clear);
                Put(3, Row(-22), Rgba.Clear);

                if (away)
                {
                    if (look.Style != HairStyle.Bald)
                    {
                        Rect(-4, Row(-31), 3, Row(look.Style == HairStyle.Short || look.Style == HairStyle.Messy ? -24 : -22), hair);
                        Put(-4, Row(-31), Rgba.Clear);
                        Put(3, Row(-31), Rgba.Clear);
                    }
                    else
                    {
                        Rect(-4, Row(-27), 3, Row(-24), hair);
                    }
                }
                else
                {
                    FaceDetails();
                    Fringe();
                }

                if (away && look.Visor != null)
                {
                    // The visor's strap round the back of the head.
                    Rect(-4, Row(-25), 3, Row(-25), Palette.Charcoal);
                }

                if (away && look.HairTips is Rgba backTips && look.Style != HairStyle.Bald)
                {
                    var tipRow = look.Style == HairStyle.Short || look.Style == HairStyle.Messy ? -24 : -22;
                    Rect(-3, Row(tipRow), 2, Row(tipRow), backTips);
                }

                switch (look.Style)
                {
                    case HairStyle.Messy:
                        Put(-3, Row(-32), hair);
                        Put(0, Row(-32), hair);
                        Put(2, Row(-32), hair);
                        break;
                    case HairStyle.Curly:
                        Rect(-5, Row(-30), -5, Row(-23), hair);
                        Rect(4, Row(-30), 4, Row(-25), hair);
                        Rect(-3, Row(-32), 2, Row(-32), hair);
                        break;
                    case HairStyle.Bob:
                        Rect(-5, Row(-29), -5, Row(-22), hair);
                        Rect(4, Row(-29), 4, Row(-24), hair);
                        break;
                    case HairStyle.Bun when away:
                        Rect(-2, Row(-33), 1, Row(-32), hair);
                        break;
                    case HairStyle.Bun:
                        // A round bun at the back of the head.
                        Rect(-6, Row(-31), -4, Row(-29), hair);
                        Put(-6, Row(-31), Rgba.Clear);
                        break;
                    case HairStyle.Ponytail when away:
                        Rect(-1, Row(-25), 0, Row(-19), hair);
                        break;
                }

                if (look.Hat is Rgba hat)
                {
                    Rect(-6, Row(-30), 5, Row(-30), hat);
                    Rect(-3, Row(-34), 2, Row(-31), hat);
                    Rect(-3, Row(-31), 2, Row(-31), look.Trim ?? Palette.Red);
                }
            }

            private void Fringe()
            {
                var hair = look.Hair;
                if (look.Style == HairStyle.Bald)
                {
                    // Grey hair round the sides only.
                    Rect(-4, Row(-27), -3, Row(-24), hair);
                    return;
                }

                // Hair over the top and down the back of the head (the left side, facing right).
                Rect(-4, Row(-31), 3, Row(-28), hair);
                Put(-4, Row(-31), Rgba.Clear);
                Put(3, Row(-31), Rgba.Clear);
                var sideEnd = look.Style == HairStyle.Short || look.Style == HairStyle.Messy ? -26 : -23;
                Rect(-4, Row(-27), -3, Row(sideEnd), hair);
                if (look.Style == HairStyle.Long)
                {
                    Rect(3, Row(-28), 3, Row(-26), hair);
                }

                if (look.HairTips is Rgba tips)
                {
                    // Dyed ends: the front of the fringe and the bottom of the hair at the side.
                    Rect(1, Row(-28), 3, Row(-28), tips);
                    Rect(-4, Row(sideEnd), -3, Row(sideEnd), tips);
                }
            }

            private void FaceDetails()
            {
                var talking = pose.Action == ResidentAction.Talk && pose.Frame == 1;
                var eye = Palette.Ebony;

                Put(0, Row(-25), eye);
                Put(2, Row(-25), eye);
                if (look.Visor is Rgba visor)
                {
                    // A glowing band across both eyes, brightest in the middle.
                    Rect(-1, Row(-25), 3, Row(-25), visor);
                    Put(1, Row(-25), visor.Mix(Palette.White, 0.45));
                }
                else if (look.Glasses)
                {
                    Put(-1, Row(-25), Palette.Charcoal);
                    Put(1, Row(-25), Palette.Charcoal);
                    Put(3, Row(-25), Palette.Charcoal);
                    Put(0, Row(-26), Palette.Charcoal);
                    Put(2, Row(-26), Palette.Charcoal);
                }

                Put(3, Row(-24), Palette.Blush);
                if (look.Beard)
                {
                    Rect(-2, Row(-23), 3, Row(-22), look.Hair);
                    Put(-2, Row(-22), look.Skin);
                }

                Put(1, Row(-23), talking ? Palette.Ebony : look.Beard ? look.Hair.Shade(0.7) : Palette.Blush.Shade(0.8));
                if (talking)
                {
                    Put(2, Row(-23), Palette.Ebony);
                }
            }

            private void HairBehind()
            {
                // Long hair and ponytails fall behind the shoulders.
                if (look.Style == HairStyle.Long)
                {
                    Rect(-5, Row(-29), 4, Row(away ? -17 : -19), look.Hair);
                }
                else if (!away)
                {
                    Rect(-6, Row(-28), -5, Row(-21), look.Hair);
                }
            }

            private void Arms()
            {
                var sleeve = Sleeve;
                var hand = look.Skin;
                var top = Row(-19);
                switch (pose.Action)
                {
                    case ResidentAction.Walk:
                    {
                        var swing = pose.Frame == 0 ? 1 : pose.Frame == 2 ? -1 : 0;
                        Arm(-6, top + swing, 7);
                        Arm(4, top - swing, 7);
                        return;
                    }

                    case ResidentAction.Talk:
                        Arm(-6, top, 7);
                        if (pose.Frame == 1)
                        {
                            // A gesture with the front hand.
                            Rect(4, top, 5, top + 3, sleeve);
                            Rect(6, top - 2, 6, top - 1, hand);
                        }
                        else
                        {
                            Arm(4, top, 7);
                        }

                        return;

                    case ResidentAction.Eat:
                        Arm(-6, top, 7);
                        if (pose.Frame == 1)
                        {
                            // Hand up to the mouth with a bite of something.
                            Rect(4, top, 5, top + 2, sleeve);
                            Put(4, top - 1, hand);
                            Put(5, top - 2, Palette.Bread);
                        }
                        else
                        {
                            Rect(4, top, 5, top + 4, sleeve);
                            Rect(5, top + 5, 6, top + 5, hand);
                            Put(6, top + 4, Palette.Bread);
                        }

                        return;

                    case ResidentAction.Read:
                        // Holding a book in front, the pages turning.
                        Rect(-5, top, -4, top + 4, sleeve);
                        Rect(4, top, 5, top + 4, sleeve);
                        Rect(1, top + 3, 6, top + 6, Palette.Red);
                        Rect(2, top + 3, 5, top + 5, pose.Frame == 1 ? Palette.White.Shade(0.94) : Palette.White);
                        Put(-3, top + 5, hand);
                        return;

                    case ResidentAction.Work:
                    {
                        // Both hands busy in front, going up and down.
                        var dip = pose.Frame == 1 ? 1 : -1;
                        Rect(-5, top, -4, top + 4, sleeve);
                        Rect(4, top, 5, top + 3, sleeve);
                        Rect(5, top + 4 + dip, 6, top + 5 + dip, hand);
                        Put(-3, top + 5, hand);
                        return;
                    }

                    case ResidentAction.Cheer:
                    case ResidentAction.Exercise:
                    {
                        var high = pose.Frame == 0;
                        if (pose.Action == ResidentAction.Exercise && !high)
                        {
                            Arm(-6, top, 7);
                            Arm(4, top, 7);
                            return;
                        }

                        // Arms up: right up high, or a wave at half height.
                        var reach = high ? 7 : 4;
                        Rect(-6, top - reach + 1, -5, top + 1, sleeve);
                        Rect(4, top - reach + 1, 5, top + 1, sleeve);
                        Rect(-6, top - reach - 1, -5, top - reach, hand);
                        Rect(4, top - reach - 1, 5, top - reach, hand);
                        return;
                    }

                    default:
                        Arm(-6, top, 7);
                        Arm(4, top, 7);
                        return;
                }
            }

            /// <summary>Sleeves are a shade darker than the body, so the arms read against it.</summary>
            private Rgba Sleeve => look.Top.Shade(0.88);

            /// <summary>An arm hanging down from the shoulder: sleeve, then the hand.</summary>
            private void Arm(int col, int top, int length)
            {
                Rect(col, top, col + 1, top + length - 1, Sleeve);
                if (look.Trim is Rgba cuff)
                {
                    Rect(col, top + length - 1, col + 1, top + length - 1, cuff);
                }

                Rect(col, top + length, col + 1, top + length, look.Skin);
            }
        }
    }
}
