/*
 *  Part of PPather
 *  Copyright Pontus Borg 2008
 *
 */

using Microsoft.Extensions.Logging;
using PatherPath;
using System.Numerics;

namespace WowTriangles
{
    /// <summary>
    /// Quadtree (splits on x and y)
    /// </summary>
    public class TriangleQuadtree
    {
        private const int SplitSize = 64;

        public Node rootNode;
        private TriangleCollection tc;

        private Vector3 min;
        private Vector3 max;

        public class Node
        {
            public Vector3 min;
            public Vector3 mid;
            public Vector3 max;

            private TriangleQuadtree tree;

            private Node parent;
            public Node[,] children; // [2,2]

            public int[] triangles;

            private readonly ILogger logger;

            public Node(TriangleQuadtree tree,
                        Vector3 min,
                        Vector3 max,
                        ILogger logger)
            {
                this.logger = logger;
                this.tree = tree;
                this.min = min;
                this.max = max;
                this.mid.X = (min.X + max.X) / 2;
                this.mid.Y = (min.Y + max.Y) / 2;
                this.mid.Z = 0;
            }

            public void Build(SimpleLinkedList triangles, int depth)
            {
                if (triangles.Count < SplitSize || depth >= 10)
                {
                    this.triangles = new int[triangles.Count];
                    SimpleLinkedList.Node rover = triangles.first;
                    int i = 0;
                    while (rover != null)
                    {
                        this.triangles[i] = rover.val;
                        rover = rover.next;
                        i++;
                    }
                    if (triangles.Count >= SplitSize)
                    {
                        Vector3 size;
                        Utils.sub(out size, ref max, ref min);
                        if (logger.IsEnabled(LogLevel.Debug))
                            logger.LogDebug("New leaf " + depth + " size: " + triangles.Count + " " + size);
                    }
                }
                else
                {
                    this.triangles = null;

                    float[] xl = new float[3] { min.X, mid.X, max.X };
                    float[] yl = new float[3] { min.Y, mid.Y, max.Y };

                    Vector3 boxhalfsize = new Vector3(
                           mid.X - min.X,
                           mid.Y - min.Y,
                           1E10f);

                    children = new Node[2, 2];

                    Vector3 vertex0;
                    Vector3 vertex1;
                    Vector3 vertex2;

                    // if (depth <= 3)
                    //     logger.WriteLine(depth + " Pre tris: " + triangles.Count);

                    int ugh = 0;
                    //foreach (int triangle in triangles)
                    for (int x = 0; x < 2; x++)
                    {
                        for (int y = 0; y < 2; y++)
                        {
                            SimpleLinkedList.Node rover = triangles.GetFirst();
                            SimpleLinkedList childTris = new SimpleLinkedList(this.logger);

                            children[x, y] = new Node(tree,
                                                         new Vector3(xl[x], yl[y], 0),
                                                         new Vector3(xl[x + 1], yl[y + 1], 0), this.logger);
                            children[x, y].parent = this;
                            int c = 0;
                            while (rover != null)
                            {
                                c++;
                                SimpleLinkedList.Node next = rover.next;
                                int triangle = rover.val;
                                tree.tc.GetTriangleVertices(triangle,
                                        out vertex0.X, out vertex0.Y, out vertex0.Z,
                                        out vertex1.X, out vertex1.Y, out vertex1.Z,
                                        out vertex2.X, out vertex2.Y, out vertex2.Z);

                                if (Utils.TestTriangleBoxIntersect(vertex0, vertex1, vertex2,
                                                                  children[x, y].mid, boxhalfsize))
                                {
                                    childTris.Steal(rover, triangles);

                                    ugh++;
                                }
                                rover = next;
                            }
                            if (c == 0)
                            {
                                children[x, y] = null; // drop that
                            }
                            else
                            {
                                //logger.WriteLine(depth + " of " + c + " stole " + childTris.RealCount + "(" + childTris.Count + ")" + " left is " + triangles.RealCount + "(" + triangles.Count + ")");
                                children[x, y].Build(childTris, depth + 1);
                                triangles.StealAll(childTris);
                            }
                            /*if (depth == 0)
                            {
                                logger.WriteLine("Post tris: " + triangles.Count);
                                logger.WriteLine("count: " + c);
                            }*/
                        }
                    }
                }
            }
        }

        private readonly ILogger logger;

        public TriangleQuadtree(TriangleCollection tc, ILogger logger)
        {
            this.logger = logger;
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Build oct " + tc.GetNumberOfTriangles());
            this.tc = tc;
            tc.GetBBox(out min.X, out min.Y, out min.Z,
                       out max.X, out max.Y, out max.Z);
            rootNode = new Node(this, min, max, this.logger);

            SimpleLinkedList tlist = new SimpleLinkedList(this.logger);
            for (int i = 0; i < tc.GetNumberOfTriangles(); i++)
            {
                tlist.AddNew(i);
            }
            rootNode.Build(tlist, 0);
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("done");
        }
    }
}