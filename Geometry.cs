using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;

namespace MinRectangle;

public static class Geometry
{
    // Векторний добуток OA × OB
    private static double Cross(Point O, Point A, Point B) =>
        (A.X - O.X) * (B.Y - O.Y) - (A.Y - O.Y) * (B.X - O.X);

    // Опукла оболонка — Graham scan, O(N log N)
    public static List<Point> GrahamScan(IEnumerable<Point> input)
    {
        var pts = input.Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
        if (pts.Count <= 1) return pts;

        var lower = new List<Point>();
        foreach (var p in pts)
        {
            while (lower.Count >= 2 && Cross(lower[^2], lower[^1], p) <= 0)
                lower.RemoveAt(lower.Count - 1);
            lower.Add(p);
        }

        var upper = new List<Point>();
        for (int i = pts.Count - 1; i >= 0; i--)
        {
            var p = pts[i];
            while (upper.Count >= 2 && Cross(upper[^2], upper[^1], p) <= 0)
                upper.RemoveAt(upper.Count - 1);
            upper.Add(p);
        }

        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);
        lower.AddRange(upper);
        return lower;
    }

    // Мінімальний охоплюючий прямокутник, O(H²)
    // Повертає (площа, 4 кути) або null якщо < 2 точок
    public static (double Area, Point[] Corners)? MinBoundingRect(List<Point> hull)
    {
        int n = hull.Count;
        if (n == 0) return null;
        if (n == 1) return (0, new[] { hull[0], hull[0], hull[0], hull[0] });
        if (n == 2) return (0, new[] { hull[0], hull[1], hull[1], hull[0] });

        double bestArea = double.MaxValue;
        Point[]? bestRect = null;

        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            double dx = hull[j].X - hull[i].X;
            double dy = hull[j].Y - hull[i].Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-10) continue;

            double ux = dx / len, uy = dy / len;
            double nx = -uy,      ny =  ux;

            double minU = double.MaxValue, maxU = double.MinValue;
            double minN = double.MaxValue, maxN = double.MinValue;

            foreach (var p in hull)
            {
                double pu = p.X * ux + p.Y * uy;
                double pn = p.X * nx + p.Y * ny;
                if (pu < minU) minU = pu;
                if (pu > maxU) maxU = pu;
                if (pn < minN) minN = pn;
                if (pn > maxN) maxN = pn;
            }

            double area = (maxU - minU) * (maxN - minN);
            if (area < bestArea)
            {
                bestArea = area;
                bestRect = new[]
                {
                    new Point(minU * ux + minN * nx, minU * uy + minN * ny),
                    new Point(maxU * ux + minN * nx, maxU * uy + minN * ny),
                    new Point(maxU * ux + maxN * nx, maxU * uy + maxN * ny),
                    new Point(minU * ux + maxN * nx, minU * uy + maxN * ny),
                };
            }
        }

        return bestRect is null ? null : (bestArea, bestRect);
    }
}
