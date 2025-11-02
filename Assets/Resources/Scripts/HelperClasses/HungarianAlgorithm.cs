using System;

public static class HungarianAlgorithm
{
    // Solve the assignment problem (minimize total cost)
    // Returns array of assignments: result[i] = index of assigned column for row i
    public static int[] Solve(float[,] costs)
    {
        int nRows = costs.GetLength(0);
        int nCols = costs.GetLength(1);
        int n = Math.Max(nRows, nCols);
        float[,] cost = new float[n, n];

        // Pad to square
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                cost[i, j] = (i < nRows && j < nCols) ? costs[i, j] : 0;

        float[] u = new float[n + 1];
        float[] v = new float[n + 1];
        int[] p = new int[n + 1];
        int[] way = new int[n + 1];

        for (int i = 1; i <= n; i++)
        {
            p[0] = i;
            int j0 = 0;
            float[] minv = new float[n + 1];
            bool[] used = new bool[n + 1];
            for (int j = 1; j <= n; j++) { minv[j] = float.MaxValue; }

            do
            {
                used[j0] = true;
                int i0 = p[j0];
                float delta = float.MaxValue;
                int j1 = 0;

                for (int j = 1; j <= n; j++)
                {
                    if (used[j]) continue;
                    float cur = cost[i0 - 1, j - 1] - u[i0] - v[j];
                    if (cur < minv[j])
                    {
                        minv[j] = cur;
                        way[j] = j0;
                    }
                    if (minv[j] < delta)
                    {
                        delta = minv[j];
                        j1 = j;
                    }
                }

                for (int j = 0; j <= n; j++)
                {
                    if (used[j])
                    {
                        u[p[j]] += delta;
                        v[j] -= delta;
                    }
                    else
                    {
                        minv[j] -= delta;
                    }
                }

                j0 = j1;
            } while (p[j0] != 0);

            do
            {
                int j1 = way[j0];
                p[j0] = p[j1];
                j0 = j1;
            } while (j0 != 0);
        }

        int[] result = new int[nRows];
        for (int j = 1; j <= n; j++)
        {
            if (p[j] <= nRows && j - 1 < nCols)
                result[p[j] - 1] = j - 1;
        }

        return result;
    }
}
