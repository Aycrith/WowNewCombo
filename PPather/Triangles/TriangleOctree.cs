/*
 *  Part of PPather
 *  Copyright Pontus Borg 2008
 *
 */

using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Numerics;

namespace WowTriangles
{
    public class TriangleOctree
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

            private TriangleOctree tree;

            private Node parent;
            public Node[,,] children; // [2,2,2]

            public int[] triangles;
            private readonly ILogger logger;

            public Node(TriangleOctree tree,
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
                this.mid.Z = (min.Z + max.Z) / 2;

                //triangles = new SimpleLinkedList();  // assume being a leaf node
            }

            public void Build(SimpleLinkedList triangles, int depth)
            {
                if (triangles.Count < SplitSize || depth >= 8)
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
                        //Vector size;
                        //Utils.sub(out size, max, min);
                        //logger.WriteLine("New leaf " + depth + " size: " + triangles.Count + " " + size);
                    }
                }
                else
                {
                    this.triangles = null;

                    float[] xl = new float[3] { min.X, mid.X, max.X };
                    float[] yl = new float[3] { min.Y, mid.Y, max.Y };
                    float[] zl = new float[3] { min.Z, mid.Z, max.Z };

                    Vector3 boxhalfsize = new Vector3(
                           mid.X - min.X,
                            mid.Y - min.Y,
                            mid.Z - min.Z);

                    // allocate children
                    //SimpleLinkedList[, ,] childTris = new SimpleLinkedList[2, 2, 2];
                    children = new Node[2, 2, 2];

                    Vector3 vertex0;
                    Vector3 vertex1;
                    Vector3 vertex2;

                    //foreach (int triangle in triangles)
                    for (int x = 0; x < 2; x++)
                    {
                        for (int y = 0; y < 2; y++)
                        {
                            for (int z = 0; z < 2; z++)
                            {
                                SimpleLinkedList.Node rover = triangles.GetFirst();
                                SimpleLinkedList childTris = new SimpleLinkedList(this.logger);

                                children[x, y, z] = new Node(tree,
                                                             new Vector3(xl[x], yl[y], zl[z]),
                                                             new Vector3(xl[x + 1], yl[y + 1], zl[z + 1]),
                                                             this.logger);
                                children[x, y, z].parent = this;
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
                                                                      children[x, y, z].mid, boxhalfsize))
                                    {
                                        childTris.Steal(rover, triangles);
                                    }
                                    rover = next;
                                }
                                if (c == 0)
                                {
                                    children[x, y, z] = null; // drop that
                                }
                                else
                                {
                                    //logger.WriteLine(depth + " of " + c + " stole " + childTris.RealCount + "(" + childTris.Count + ")" + " left is " + triangles.RealCount + "(" + triangles.Count + ")");
                                    children[x, y, z].Build(childTris, depth + 1);
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

            public void FindTrianglesInBox(Vector3 box_min, Vector3 box_max, HashSet<int> found)
            {
                if (triangles != null)
                {
                    foreach(int tri in triangles)
                        found.Add(tri);
                }
                else
                {
                    for (int x = 0; x < 2; x++)
                    {
                        for (int y = 0; y < 2; y++)
                        {
                            for (int z = 0; z < 2; z++)
                            {
                                Node child = children[x, y, z];
                                if (child != null)
                                {
                                    if (Utils.TestBoxBoxIntersect(box_min, box_max, child.min, child.max))
                                        child.FindTrianglesInBox(box_min, box_max, found);
                                }
                            }
                        }
                    }
                }
            }
        }

        public HashSet<int> FindTrianglesInBox(float min_x, float min_y, float min_z,
                                           float max_x, float max_y, float max_z)
        {
            Vector3 min = new(min_x, min_y, min_z);
            Vector3 max = new(max_x, max_y, max_z);
            HashSet<int> found = new();
            rootNode.FindTrianglesInBox(min, max, found);
            return found;
        }

        private readonly ILogger logger;

        public TriangleOctree(TriangleCollection tc, ILogger logger)
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