using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;

using System;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Diagnostics;
namespace PluginCommands
{
    public class PluginCommands
    {
        [CommandMethod("PS_CreateSquare")]
        public void CreateSquare()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            var database = document.Database;
            var editor = document.Editor;

            // Prompt for the side length of the square
            PromptDoubleResult promptResult = editor.GetDouble("\nEnter side length of the square: ");
            if (promptResult.Status != PromptStatus.OK)
                return;

            double sideLength = promptResult.Value;

            // Prompt for the insertion point of the square
            PromptPointResult pointResult = editor.GetPoint("\nSpecify insertion point: ");
            if (pointResult.Status != PromptStatus.OK)
                return;

            // Get the point where the user clicked
            Point3d insertionPoint = pointResult.Value;

            // Create the square in the current space (Model or Paper space)
            using (Transaction transaction = database.TransactionManager.StartTransaction())
            {
                BlockTable blockTable = transaction.GetObject(database.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord currentSpace = transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                // Define the square as a polyline
                Polyline square = new Polyline();
                square.AddVertexAt(0, new Point2d(insertionPoint.X, insertionPoint.Y), 0, 0, 0);
                square.AddVertexAt(1, new Point2d(insertionPoint.X + sideLength, insertionPoint.Y), 0, 0, 0);
                square.AddVertexAt(2, new Point2d(insertionPoint.X + sideLength, insertionPoint.Y + sideLength), 0, 0, 0);
                square.AddVertexAt(3, new Point2d(insertionPoint.X, insertionPoint.Y + sideLength), 0, 0, 0);
                square.Closed = true;

                // Add the square to the current space
                currentSpace.AppendEntity(square);
                transaction.AddNewlyCreatedDBObject(square, true);

                // Commit the transaction
                transaction.Commit();
            }

            editor.WriteMessage($"\nSquare with side length {sideLength} created at ({insertionPoint.X}, {insertionPoint.Y}).");
        }


        [CommandMethod("PS_AlignObjects")]
        public void AlignObjects()
        {
            var document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            var editor = document.Editor;

            // Prompt user to select objects
            PromptSelectionResult selectionResult = editor.GetSelection();
            if (selectionResult.Status != PromptStatus.OK)
                return;

            // Get the selected objects
            SelectionSet selectionSet = selectionResult.Value;
            ObjectId[] objectIds = selectionSet.GetObjectIds();

            // Prompt user to select alignment type (horizontal or vertical)
            PromptKeywordOptions alignmentPromptOptions = new PromptKeywordOptions("\nSelect alignment direction [Horizontal/Vertical]:");
            alignmentPromptOptions.Keywords.Add("Horizontal");
            alignmentPromptOptions.Keywords.Add("Vertical");
            alignmentPromptOptions.Keywords.Default = "Horizontal";
            PromptResult alignmentPromptResult = editor.GetKeywords(alignmentPromptOptions);
            if (alignmentPromptResult.Status != PromptStatus.OK)
                return;

            string alignmentDirection = alignmentPromptResult.StringResult;
            bool isHorizontal = (alignmentDirection == "Horizontal");

            // Determine the offset distance based on user input
            PromptDoubleOptions offsetPromptOptions = new PromptDoubleOptions($"\nEnter offset distance (in {(isHorizontal ? "X" : "Y")} direction):");
            offsetPromptOptions.AllowNegative = true;
            offsetPromptOptions.AllowZero = true; // Allow zero spacing if needed
            PromptDoubleResult offsetPromptResult = editor.GetDouble(offsetPromptOptions);
            if (offsetPromptResult.Status != PromptStatus.OK)
                return;

            double offsetDistance = offsetPromptResult.Value;

            // Align the selected objects
            using (Transaction transaction = document.TransactionManager.StartTransaction())
            {
                BlockTableRecord currentSpace = transaction.GetObject(document.Database.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;

                // Get the base points and dimensions for sorting
                List<(ObjectId Id, Point3d Point, double Width, double Height)> objectPoints = new List<(ObjectId, Point3d, double, double)>();

                foreach (ObjectId objectId in objectIds)
                {
                    Entity entity = transaction.GetObject(objectId, OpenMode.ForRead) as Entity;
                    Point3d basePoint = GetBasePoint(entity);
                    var (width, height) = GetBlockReferenceDimensions(entity, transaction);
                    objectPoints.Add((objectId, basePoint, width, height));
                }

                // Sort the objects based on the specified direction
                objectPoints.Sort((a, b) => isHorizontal ? a.Point.X.CompareTo(b.Point.X) : a.Point.Y.CompareTo(b.Point.Y));

                // Determine the reference point for alignment
                Point3d referencePoint = isHorizontal ? objectPoints[0].Point : objectPoints[0].Point;

                // Perform alignment and spacing
                double currentOffset = 0;

                foreach ((ObjectId objectId, Point3d basePoint, double width, double height) in objectPoints)
                {
                    Entity entity = transaction.GetObject(objectId, OpenMode.ForWrite) as Entity;
                    double offsetValue = isHorizontal ? referencePoint.Y - basePoint.Y : referencePoint.X - basePoint.X;

                    // Apply translation to align the object
                    Matrix3d translation = isHorizontal
                        ? Matrix3d.Displacement(new Vector3d(currentOffset - basePoint.X + referencePoint.X, offsetValue, 0))
                        : Matrix3d.Displacement(new Vector3d(offsetValue, currentOffset - basePoint.Y + referencePoint.Y, 0));

                    entity.TransformBy(translation);

                    // Update current offset for next object
                    currentOffset += offsetDistance + (isHorizontal ? height : width);
                }

                // Commit the transaction
                transaction.Commit();
            }

            editor.WriteMessage($"\nObjects aligned {alignmentDirection.ToLower()}ly with specified offset.");
        }

        // Helper method to get the base point of an entity
        private Point3d GetBasePoint(Entity entity)
        {
            if (entity is BlockReference blockRef)
            {
                return blockRef.Position;
            }
            else if (entity is Circle circle)
            {
                return circle.Center;
            }
            else if (entity is Arc arc)
            {
                return arc.Center;
            }
            else if (entity is Polyline polyline)
            {
                return polyline.StartPoint;
            }
            else if (entity is Line line)
            {
                return line.StartPoint;
            }
            // Add more cases as needed for other entity types
            else
            {
                // Default to using the minimum point if no specific base point is found
                return entity.GeometricExtents.MinPoint;
            }
        }

        // Helper method to get the dimensions of a block reference based on its vertical and horizontal lines yes
        private (double Width, double Height) GetBlockReferenceDimensions(Entity entity, Transaction transaction)
        {
            double width = 0;
            double height = 0;

            if (entity is BlockReference blockRef)
            {
                BlockTableRecord blockDef = transaction.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                if (blockDef != null)
                {
                    foreach (ObjectId entId in blockDef)
                    {
                        Entity ent = transaction.GetObject(entId, OpenMode.ForRead) as Entity;
                        if (ent is Line line)
                        {
                            // Transform the line to the block reference's coordinate system
                            Line transformedLine = (Line)line.Clone();
                            transformedLine.TransformBy(blockRef.BlockTransform);

                            // Calculate width and height from the line endpoints
                            if (Math.Abs(transformedLine.StartPoint.X - transformedLine.EndPoint.X) < Tolerance.Global.EqualPoint)
                            {
                                // Vertical line
                                height = Math.Max(height, Math.Abs(transformedLine.StartPoint.Y - transformedLine.EndPoint.Y));
                            }
                            if (Math.Abs(transformedLine.StartPoint.Y - transformedLine.EndPoint.Y) < Tolerance.Global.EqualPoint)
                            {
                                // Horizontal line
                                width = Math.Max(width, Math.Abs(transformedLine.StartPoint.X - transformedLine.EndPoint.X));
                            }
                        }
                        else if (ent is Polyline polyline)
                        {
                            for (int i = 0; i < polyline.NumberOfVertices - 1; i++)
                            {
                                Point3d pt1 = polyline.GetPoint3dAt(i);
                                Point3d pt2 = polyline.GetPoint3dAt(i + 1);

                                // Calculate width and height from the polyline vertices
                                if (Math.Abs(pt1.X - pt2.X) < Tolerance.Global.EqualPoint)
                                {
                                    // Vertical segment
                                    height = Math.Max(height, Math.Abs(pt1.Y - pt2.Y));
                                }
                                if (Math.Abs(pt1.Y - pt2.Y) < Tolerance.Global.EqualPoint)
                                {
                                    // Horizontal segment
                                    width = Math.Max(width, Math.Abs(pt1.X - pt2.X));
                                }
                            }
                        }
                        // Add more cases for other line-based entities if needed
                    }
                }
            }

            return (width, height);
        }








        [CommandMethod("CreateDeviceBox")]
        public void CreateDeviceBox()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            // Prompt for number of inputs and outputs
            PromptIntegerOptions inputsPromptOptions = new PromptIntegerOptions("\nEnter number of inputs:");
            inputsPromptOptions.AllowZero = false;
            PromptIntegerResult inputsPromptResult = ed.GetInteger(inputsPromptOptions);
            if (inputsPromptResult.Status != PromptStatus.OK)
                return;

            int numInputs = inputsPromptResult.Value;

            PromptIntegerOptions outputsPromptOptions = new PromptIntegerOptions("\nEnter number of outputs:");
            outputsPromptOptions.AllowZero = false;
            PromptIntegerResult outputsPromptResult = ed.GetInteger(outputsPromptOptions);
            if (outputsPromptResult.Status != PromptStatus.OK)
                return;

            int numOutputs = outputsPromptResult.Value;

            // Calculate box dimensions based on inputs and outputs
            double boxWidth = 8.0; // Default width
            double boxHeight = 2.0 + Math.Max(numInputs, numOutputs) * 1.5; // Adjusted height

            // Prompt for box label
            PromptStringOptions boxLabelPromptOptions = new PromptStringOptions("\nEnter box label:");
            PromptResult boxLabelPromptResult = ed.GetString(boxLabelPromptOptions);
            if (boxLabelPromptResult.Status != PromptStatus.OK)
                return;

            string boxLabel = boxLabelPromptResult.StringResult;

            // Start a transaction to create the box and labels
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                // Create the box
                Point3d corner = new Point3d(0, 0, 0); // Default corner point
                Polyline box = new Polyline();
                box.AddVertexAt(0, new Point2d(corner.X, corner.Y), 0, 0, 0);
                box.AddVertexAt(1, new Point2d(corner.X + boxWidth, corner.Y), 0, 0, 0);
                box.AddVertexAt(2, new Point2d(corner.X + boxWidth, corner.Y + boxHeight), 0, 0, 0);
                box.AddVertexAt(3, new Point2d(corner.X, corner.Y + boxHeight), 0, 0, 0);
                box.Closed = true;
                btr.AppendEntity(box);
                tr.AddNewlyCreatedDBObject(box, true);

                // Calculate text scale based on box dimensions
                double textScale = Math.Min(boxWidth, boxHeight) / 10.0;

                // Add inputs (lines going in from left)
                double inputSpacing = boxHeight / (numInputs + 1);
                for (int i = 0; i < numInputs; i++)
                {
                    Point3d startPoint = new Point3d(corner.X, corner.Y + (i + 1) * inputSpacing, corner.Z);
                    Point3d endPoint = new Point3d(corner.X - 2.0, corner.Y + (i + 1) * inputSpacing, corner.Z);
                    Line inputLine = new Line(startPoint, endPoint);
                    btr.AppendEntity(inputLine);
                    tr.AddNewlyCreatedDBObject(inputLine, true);

                    // Add label for input inside the box
                    MText inputLabel = new MText();
                    inputLabel.Contents = $"In {i + 1}";
                    inputLabel.Location = new Point3d(corner.X - 1.0, corner.Y + (i + 1) * inputSpacing, corner.Z);
                    inputLabel.TextHeight = textScale;
                    btr.AppendEntity(inputLabel);
                    tr.AddNewlyCreatedDBObject(inputLabel, true);
                }

                // Add outputs (lines going out from right)
                double outputSpacing = boxHeight / (numOutputs + 1);
                for (int i = 0; i < numOutputs; i++)
                {
                    Point3d startPoint = new Point3d(corner.X + boxWidth, corner.Y + (i + 1) * outputSpacing, corner.Z);
                    Point3d endPoint = new Point3d(corner.X + boxWidth + 2.0, corner.Y + (i + 1) * outputSpacing, corner.Z);
                    Line outputLine = new Line(startPoint, endPoint);
                    btr.AppendEntity(outputLine);
                    tr.AddNewlyCreatedDBObject(outputLine, true);

                    // Add label for output inside the box
                    MText outputLabel = new MText();
                    outputLabel.Contents = $"Out {i + 1}";
                    outputLabel.Location = new Point3d(corner.X + boxWidth + 1.0, corner.Y + (i + 1) * outputSpacing, corner.Z);
                    outputLabel.TextHeight = textScale;
                    btr.AppendEntity(outputLabel);
                    tr.AddNewlyCreatedDBObject(outputLabel, true);
                }

                // Add label for the box
                MText boxTextLabel = new MText();
                boxTextLabel.Contents = boxLabel;
                // Calculate leftmost point for the box label
                Point3d boxTextStart = new Point3d(corner.X - boxTextLabel.ActualWidth / 2.0, corner.Y + boxHeight + textScale * 2.0, corner.Z);
                boxTextLabel.Location = boxTextStart;
                boxTextLabel.TextHeight = textScale * 1.5; // Larger text for box label
                btr.AppendEntity(boxTextLabel);
                tr.AddNewlyCreatedDBObject(boxTextLabel, true);

                // Combine all entities into a block for easier selection
                DBObjectCollection entityObjs = new DBObjectCollection();
                entityObjs.Add(box);
                foreach (ObjectId id in btr)
                {
                    entityObjs.Add(id.GetObject(OpenMode.ForRead));
                }

                // Create a block table record for the block
                BlockTableRecord blockRecord = new BlockTableRecord();
                blockRecord.Name = "*U"; // Unique name
                bt.UpgradeOpen();
                ObjectId blockId = bt.Add(blockRecord);
                tr.AddNewlyCreatedDBObject(blockRecord, true);

                // Insert the block reference
                BlockReference blockRef = new BlockReference(boxTextStart, blockId);
                btr.AppendEntity(blockRef);
                tr.AddNewlyCreatedDBObject(blockRef, true);

                // Commit the transaction
                tr.Commit();
            }

            ed.WriteMessage($"\nDevice box '{boxLabel}' created with {numInputs} inputs and {numOutputs} outputs.");
        }

        [CommandMethod("PS_FanOutLines")]
        public void FanOutLines()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            var editor = document.Editor;

            // Prompt user to select the main polyline
            PromptEntityOptions polylinePromptOptions = new PromptEntityOptions("\nSelect a polyline:");
            polylinePromptOptions.SetRejectMessage("\nSelected entity is not a polyline.");
            polylinePromptOptions.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult polylinePromptResult = editor.GetEntity(polylinePromptOptions);
            if (polylinePromptResult.Status != PromptStatus.OK)
                return;

            ObjectId polylineId = polylinePromptResult.ObjectId;

            // Prompt user to select connection points
            PromptPointOptions pointPromptOptions = new PromptPointOptions("\nSelect points to connect (press Enter to finish):");
            pointPromptOptions.AllowNone = true;

            List<Point3d> connectionPoints = new List<Point3d>();
            while (true)
            {
                PromptPointResult pointPromptResult = editor.GetPoint(pointPromptOptions);
                if (pointPromptResult.Status == PromptStatus.None || pointPromptResult.Status == PromptStatus.Cancel)
                    break;

                if (pointPromptResult.Status == PromptStatus.OK)
                {
                    connectionPoints.Add(pointPromptResult.Value);
                }
            }

            if (connectionPoints.Count == 0)
            {
                editor.WriteMessage("\nNo connection points selected.");
                return;
            }

            using (Transaction transaction = document.TransactionManager.StartTransaction())
            {
                Polyline polyline = transaction.GetObject(polylineId, OpenMode.ForRead) as Polyline;
                BlockTable blockTable = transaction.GetObject(document.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord blockTableRecord = transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                foreach (Point3d connectionPoint in connectionPoints)
                {
                    // Find the closest point on the polyline to the connection point
                    Point3d closestPoint = FindClosestPointOnPolyline(polyline, connectionPoint, transaction);

                    // Find the path to the closest point on the polyline
                    List<Line> pathLines = FindPathToClosestPoint(closestPoint, connectionPoint, transaction);

                    // Create new lines for the path
                    foreach (Line line in pathLines)
                    {
                        blockTableRecord.AppendEntity(line);
                        transaction.AddNewlyCreatedDBObject(line, true);
                    }
                }

                // Commit the transaction
                transaction.Commit();
            }

            editor.WriteMessage("\nLines created connecting to the selected points.");
        }

        // Helper method to find the closest point on a polyline to a given point
        private Point3d FindClosestPointOnPolyline(Polyline polyline, Point3d point, Transaction transaction)
        {
            Point3d closestPoint = new Point3d();
            double minDistance = double.MaxValue;

            for (int i = 0; i < polyline.NumberOfVertices - 1; i++)
            {
                Point3d start = polyline.GetPoint3dAt(i);
                Point3d end = polyline.GetPoint3dAt(i + 1);

                LineSegment3d segment = new LineSegment3d(start, end);
                PointOnCurve3d closestPointOnSegment = segment.GetClosestPointTo(point);

                Point3d closestPoint3dOnSegment = closestPointOnSegment.Point;
                double distance = closestPoint3dOnSegment.DistanceTo(point);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestPoint = closestPoint3dOnSegment;
                }
            }

            return closestPoint;
        }

        // Helper method to retrieve obstacles in the drawing
        private List<Entity> GetObstacles(Transaction transaction)
        {
            List<Entity> obstacles = new List<Entity>();
            BlockTable blockTable = transaction.GetObject(Application.DocumentManager.MdiActiveDocument.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
            BlockTableRecord blockTableRecord = transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;

            foreach (ObjectId id in blockTableRecord)
            {
                Entity entity = transaction.GetObject(id, OpenMode.ForRead) as Entity;
                // Exclude lines, we will handle them separately
                if (entity != null && !(entity is Line))
                {
                    obstacles.Add(entity);
                }
            }

            return obstacles;
        }

        // Helper method to find the next point in the path to avoid obstacles
        private Point3d FindNextPoint(Point3d currentPoint, Point3d targetPoint, List<Entity> obstacles)
        {
            // Implement a simple grid-based approach or use A* algorithm to avoid obstacles and move horizontally or vertically
            // For simplicity, let's assume a basic implementation

            // Example implementation that moves horizontally or vertically
            //if (Math.Abs(targetPoint.X - currentPoint.X) > Math.Abs(targetPoint.Y - currentPoint.Y))
            if (targetPoint.X != currentPoint.X && targetPoint.Y != currentPoint.Y)
            {
                return new Point3d(currentPoint.X, targetPoint.Y, currentPoint.Z);
            }
            else
            {
                return new Point3d(targetPoint.X, currentPoint.Y, currentPoint.Z);
            }
        }

        // Helper method to find the path to the closest point on the polyline
        private List<Line> FindPathToClosestPoint(Point3d startPoint, Point3d endPoint, Transaction transaction)
        {
            //var document2 = Application.DocumentManager.MdiActiveDocument;
            //var editor2 = document2.Editor;
            List<Line> pathLines = new List<Line>();

            // Create a new pathfinding grid
            // Assume `GetObstacles` method retrieves all objects in the drawing that should be considered obstacles
            List<Entity> obstacles = GetObstacles(transaction);
            /*foreach (Entity entity in obstacles)
            {
                editor2.WriteMessage(entity.ToString() + "/n");
            }*/

            Point3d currentPoint = startPoint;
            Point3d targetPoint = endPoint;

            // Pathfinding logic
            while (currentPoint != targetPoint)
            {
                // Find the next point to move to (either horizontally or vertically)
                Point3d nextPoint = FindNextPoint(currentPoint, targetPoint, obstacles);

                // Create a line from current point to next point
                Line pathLine = new Line(currentPoint, nextPoint);
                pathLines.Add(pathLine);

                // Update the current point
                currentPoint = nextPoint;
            }

            // Add the final line to the end point
            Line finalLine = new Line(currentPoint, targetPoint);
            pathLines.Add(finalLine);

            return pathLines;
        }









        [CommandMethod("ConnectPoints")]
        public void ConnectPoints()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                // Prompt user for the start point
                PromptPointResult ppr1 = ed.GetPoint("Select the start point: ");
                if (ppr1.Status != PromptStatus.OK) return;
                Point3d startPoint = ppr1.Value;

                // Prompt user for the end point
                PromptPointResult ppr2 = ed.GetPoint("Select the end point: ");
                if (ppr2.Status != PromptStatus.OK) return;
                Point3d endPoint = ppr2.Value;

                // Get bounding boxes of all objects in the drawing
                List<Extents3d> obstacles = new List<Extents3d>();
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                    foreach (ObjectId objId in btr)
                    {
                        Entity entity = (Entity)tr.GetObject(objId, OpenMode.ForRead);
                        if (entity.GeometricExtents != null)
                        {
                            obstacles.Add(entity.GeometricExtents);
                        }
                    }
                    tr.Commit();
                }

                // Implement pathfinding algorithm
                List<Point3d> path = FindPath(startPoint, endPoint, obstacles);

                if (path == null)
                {
                    ed.WriteMessage("No path found.");
                    return;
                }

                // Draw the path
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    Polyline polyline = new Polyline();
                    for (int i = 0; i < path.Count; i++)
                    {
                        polyline.AddVertexAt(i, new Point2d(path[i].X, path[i].Y), 0, 0, 0);
                    }

                    btr.AppendEntity(polyline);
                    tr.AddNewlyCreatedDBObject(polyline, true);
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("Error: " + ex.Message);
            }
        }

        private List<Point3d> FindPath(Point3d start, Point3d end, List<Extents3d> obstacles)
        {
            // Implement your pathfinding algorithm here
            List<Point3d> path = new List<Point3d>();

            // Using a simple A* algorithm with constraints
            PriorityQueue<Node> openSet = new PriorityQueue<Node>();
            Dictionary<Point3d, Node> allNodes = new Dictionary<Point3d, Node>();

            Node startNode = new Node(start, null, 0, GetHeuristic(start, end));
            openSet.Enqueue(startNode);
            allNodes[start] = startNode;

            while (openSet.Count > 0)
            {
                Node current = openSet.Dequeue();

                if (current.Point.IsEqualTo(end))
                {
                    Node temp = current;
                    while (temp != null)
                    {
                        path.Add(temp.Point);
                        temp = temp.Parent;
                    }
                    path.Reverse();
                    return path;
                }

                foreach (var neighbor in GetNeighbors(current.Point, obstacles))
                {
                    double tentativeGScore = current.GScore + GetDistance(current.Point, neighbor);
                    if (!allNodes.ContainsKey(neighbor) || tentativeGScore < allNodes[neighbor].GScore)
                    {
                        Node neighborNode = new Node(neighbor, current, tentativeGScore, GetHeuristic(neighbor, end));
                        openSet.Enqueue(neighborNode);
                        allNodes[neighbor] = neighborNode;
                    }
                }
            }

            // If we get here, no path was found
            return null;
        }

        private List<Point3d> GetNeighbors(Point3d point, List<Extents3d> obstacles)
        {
            List<Point3d> neighbors = new List<Point3d>
            {
                new Point3d(point.X + 1, point.Y, 0),
                new Point3d(point.X - 1, point.Y, 0),
                new Point3d(point.X, point.Y + 1, 0),
                new Point3d(point.X, point.Y - 1, 0)
            };

            // Remove neighbors that are within an obstacle
            neighbors.RemoveAll(n => IsPointInObstacle(n, obstacles));

            return neighbors;
        }

        private bool IsPointInObstacle(Point3d point, List<Extents3d> obstacles)
        {
            foreach (var obstacle in obstacles)
            {
                if (point.X >= obstacle.MinPoint.X && point.X <= obstacle.MaxPoint.X &&
                    point.Y >= obstacle.MinPoint.Y && point.Y <= obstacle.MaxPoint.Y)
                {
                    return true;
                }
            }
            return false;
        }

        private double GetDistance(Point3d a, Point3d b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y); // Manhattan distance for grid-based pathfinding
        }

        private double GetHeuristic(Point3d a, Point3d b)
        {
            return GetDistance(a, b); // Same as distance for now
        }

        private class Node : IComparable<Node>
        {
            public Point3d Point { get; }
            public Node Parent { get; }
            public double GScore { get; }
            public double FScore { get; }

            public Node(Point3d point, Node parent, double gScore, double fScore)
            {
                Point = point;
                Parent = parent;
                GScore = gScore;
                FScore = fScore;
            }

            public int CompareTo(Node other)
            {
                return FScore.CompareTo(other.FScore);
            }
        }

        private class PriorityQueue<T> where T : IComparable<T>
        {
            private List<T> data;

            public PriorityQueue()
            {
                this.data = new List<T>();
            }

            public void Enqueue(T item)
            {
                data.Add(item);
                int ci = data.Count - 1; // child index; start at end
                while (ci > 0)
                {
                    int pi = (ci - 1) / 2; // parent index
                    if (data[ci].CompareTo(data[pi]) >= 0) break; // child item is larger than (or equal) parent so we're done
                    T tmp = data[ci]; data[ci] = data[pi]; data[pi] = tmp;
                    ci = pi;
                }
            }

            public T Dequeue()
            {
                // Assumes pq is not empty
                int li = data.Count - 1; // last index (before removal)
                T frontItem = data[0];   // fetch the front
                data[0] = data[li];
                data.RemoveAt(li);

                --li; // last index (after removal)
                int pi = 0; // parent index. start at front of pq
                while (true)
                {
                    int ci = pi * 2 + 1; // left child index of parent
                    if (ci > li) break;  // no children so done
                    int rc = ci + 1;     // right child
                    if (rc <= li && data[rc].CompareTo(data[ci]) < 0) // if there is a right child (rc <= li) and it is smaller
                        ci = rc; // use the right child instead

                    if (data[pi].CompareTo(data[ci]) <= 0) break; // parent is smaller than (or equal to) smallest child so done
                    T tmp = data[pi]; data[pi] = data[ci]; data[ci] = tmp; // swap parent and child
                    pi = ci;
                }
                return frontItem;
            }

            public int Count
            {
                get { return data.Count; }
            }
        }





        /*[CommandMethod("RemoveIECTextAndAdjacentText")]
        public void RemoveIECTextAndAdjacentText()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // Prompt for selecting a block reference
                PromptEntityOptions blockOptions = new PromptEntityOptions("\nSelect a block reference: ");
                blockOptions.SetRejectMessage("\nOnly block references are allowed.");
                blockOptions.AddAllowedClass(typeof(BlockReference), true);
                PromptEntityResult blockResult = ed.GetEntity(blockOptions);

                if (blockResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\nCommand canceled.");
                    return;
                }

                // Get the block reference and its attributes
                BlockReference blockRef = (BlockReference)tr.GetObject(blockResult.ObjectId, OpenMode.ForWrite);

                // Explode the block reference to access its entities
                DBObjectCollection explodedObjects = new DBObjectCollection();
                blockRef.Explode(explodedObjects);

                // Add exploded objects to the model space
                BlockTableRecord currentSpace = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                foreach (DBObject obj in explodedObjects)
                {
                    if (obj is Entity entity)
                    {
                        currentSpace.AppendEntity(entity);
                        tr.AddNewlyCreatedDBObject(entity, true);
                    }
                }

                // Find the "IEC" text and the closest horizontal line
                DBText iecText = null;
                Line closestLine = null;
                double minDistance = double.MaxValue;

                foreach (DBObject obj in explodedObjects)
                {
                    if (obj is DBText text)
                    {
                        if (text.TextString == "IEC")
                        {
                            iecText = text;
                            // Find the closest horizontal line to this text
                            foreach (DBObject innerObj in explodedObjects)
                            {
                                if (innerObj is Line line && IsHorizontal(line))
                                {
                                    double distance = DistanceToLine(text.Position, line);
                                    if (distance < minDistance)
                                    {
                                        minDistance = distance;
                                        closestLine = line;
                                    }
                                }
                            }
                        }
                    }
                }

                // Remove the IEC text and the closest horizontal line if found
                if (iecText != null)
                {
                    iecText.UpgradeOpen(); // Ensure the text can be erased
                    iecText.Erase();
                }

                if (closestLine != null)
                {
                    closestLine.UpgradeOpen(); // Ensure the line can be erased
                    closestLine.Erase();
                }

                // Find and remove the single closest text to the right or left of the removed line
                DBText closestText = null;
                double minSideDistance = double.MaxValue;

                foreach (DBObject obj in explodedObjects)
                {
                    if (obj is DBText text && text != iecText) // Skip the already removed "IEC" text
                    {
                        double distance = DistanceToSideOfLine(text.Position, closestLine);
                        if (distance < minSideDistance)
                        {
                            minSideDistance = distance;
                            closestText = text;
                        }
                    }
                }

                // Remove the closest text found if it exists
                if (closestText != null)
                {
                    if (closestText.IsWriteEnabled)
                    {
                        closestText.Erase();
                    }
                    else
                    {
                        closestText.UpgradeOpen();
                        closestText.Erase();
                    }
                }

                tr.Commit();
            }
        }

        private bool IsHorizontal(Line line)
        {
            return line.StartPoint.Y == line.EndPoint.Y;
        }

        private double DistanceToLine(Point3d point, Line line)
        {
            // Calculate distance from point to the line segment
            double dx = line.EndPoint.X - line.StartPoint.X;
            double dy = line.EndPoint.Y - line.StartPoint.Y;
            double lengthSquared = dx * dx + dy * dy;
            if (lengthSquared == 0) return point.DistanceTo(line.StartPoint);

            double t = ((point.X - line.StartPoint.X) * dx + (point.Y - line.StartPoint.Y) * dy) / lengthSquared;
            t = Math.Max(0, Math.Min(1, t));
            Point3d projection = new Point3d(line.StartPoint.X + t * dx, line.StartPoint.Y + t * dy, 0);
            return point.DistanceTo(projection);
        }

        private double DistanceToSideOfLine(Point3d textPosition, Line line)
        {
            // Calculate the distance from the text to the side of the line
            if (IsHorizontal(line))
            {
                return Math.Abs(textPosition.Y - line.StartPoint.Y);
            }
            else
            {
                return Math.Abs(textPosition.X - line.StartPoint.X);
            }
        }*/






        /*[CommandMethod("RemoveIECTextAndRecreateBlock")]
        public void RemoveIECTextAndRecreateBlock()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // Prompt for selecting a block reference
                PromptEntityOptions blockOptions = new PromptEntityOptions("\nSelect a block reference: ");
                blockOptions.SetRejectMessage("\nOnly block references are allowed.");
                blockOptions.AddAllowedClass(typeof(BlockReference), true);
                PromptEntityResult blockResult = ed.GetEntity(blockOptions);

                if (blockResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\nCommand canceled.");
                    return;
                }

                // Get the block reference and its attributes
                BlockReference blockRef = (BlockReference)tr.GetObject(blockResult.ObjectId, OpenMode.ForWrite);

                // Print attributes of the block reference
                BlockTableRecord blockDef = (BlockTableRecord)tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead);
                foreach (ObjectId attrId in blockDef)
                {
                    DBObject obj = tr.GetObject(attrId, OpenMode.ForRead);
                    if (obj is AttributeDefinition attrDef)
                    {
                        ed.WriteMessage($"\nAttribute Definition: Tag = {attrDef.Tag}, Default Value = {attrDef.TextString}");
                    }
                    else if (obj is AttributeReference attrRef)
                    {
                        ed.WriteMessage($"\nAttribute Reference: Tag = {attrRef.Tag}, Value = {attrRef.TextString}");
                    }
                    else if (obj is Entity entity)
                    {
                        ed.WriteMessage($"\n  Entity in Block Definition: Type = {entity.GetType().Name}");
                    }
                }
                // Explode the block reference to access its entities
                DBObjectCollection explodedObjects = new DBObjectCollection();
                blockRef.Explode(explodedObjects);

                // Add exploded objects to the model space and print their information
                BlockTableRecord currentSpace = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                foreach (DBObject obj in explodedObjects)
                {
                    if (obj is Entity entity)
                    {
                        currentSpace.AppendEntity(entity);
                        tr.AddNewlyCreatedDBObject(entity, true);

                        // Print information about each exploded entity
                        if (entity is DBText text)
                        {
                            ed.WriteMessage($"\nExploded Entity - DBText: Tag = {text.TextString}, Position = {text.Position}");
                        }
                        else if (entity is Line line)
                        {
                            ed.WriteMessage($"\nExploded Entity - Line: Start = {line.StartPoint}, End = {line.EndPoint}");
                        }
                        else if (entity is Circle circle)
                        {
                            ed.WriteMessage($"\nExploded Entity - Circle: Center = {circle.Center}, Radius = {circle.Radius}");
                        }
                        // Add more else if blocks here to handle other types of entities if necessary
                        else
                        {
                            ed.WriteMessage($"\nExploded Entity - Other: {entity.GetType().Name}");
                        }
                    }
                }
                // Find the "IEC" text and the closest horizontal line
                DBText iecText = null;
                Line closestLine = null;
                double minDistance = double.MaxValue;

                foreach (DBObject obj in explodedObjects)
                {
                    if (obj is DBText text)
                    {
                        if (text.TextString == "IEC")
                        {
                            iecText = text;
                            // Find the closest horizontal line to this text
                            foreach (DBObject innerObj in explodedObjects)
                            {
                                if (innerObj is Line line && IsHorizontal(line))
                                {
                                    double distance = DistanceToLine(text.Position, line);
                                    if (distance < minDistance)
                                    {
                                        minDistance = distance;
                                        closestLine = line;
                                    }
                                }
                            }
                        }
                    }
                }

                // Remove the IEC text and the closest horizontal line if found
                if (iecText != null)
                {
                    iecText.UpgradeOpen(); // Ensure the text can be erased
                    iecText.Erase();
                }

                if (closestLine != null)
                {
                    closestLine.UpgradeOpen(); // Ensure the line can be erased
                    closestLine.Erase();
                }

                // Find and remove the single closest text to the right or left of the removed line
                DBText closestText = null;
                double minSideDistance = double.MaxValue;

                foreach (DBObject obj in explodedObjects)
                {
                    if (obj is DBText text && text != iecText) // Skip the already removed "IEC" text
                    {
                        double distance = DistanceToSideOfLine(text.Position, closestLine);
                        if (distance < minSideDistance)
                        {
                            minSideDistance = distance;
                            closestText = text;
                        }
                    }
                }

                // Remove the closest text found if it exists
                if (closestText != null)
                {
                    if (closestText.IsWriteEnabled)
                    {
                        closestText.Erase();
                    }
                    else
                    {
                        closestText.UpgradeOpen();
                        closestText.Erase();
                    }
                }

                // Move the remaining entities to avoid overlap
                Vector3d moveVector = new Vector3d(10, 0, 0); // Adjust the vector as needed

                BlockTableRecord currentSpace2 = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                foreach (DBObject obj in explodedObjects)
                {
                    if (obj is Entity entity)
                    {
                        // Apply the translation to avoid overlap
                        entity.TransformBy(Matrix3d.Displacement(moveVector));

                        currentSpace2.AppendEntity(entity);
                        tr.AddNewlyCreatedDBObject(entity, true);
                    }
                }

                // Create a new block definition with the remaining entities
                string newBlockName = "UpdatedBlock_" + blockRef.GetHashCode(); // Unique block name
                BlockTable blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
                BlockTableRecord newBlockRecord = new BlockTableRecord();
                newBlockRecord.Name = newBlockName;
                blockTable.Add(newBlockRecord);
                tr.AddNewlyCreatedDBObject(newBlockRecord, true);

                // Add entities to the new block record
                foreach (DBObject obj in explodedObjects)
                {
                    if (obj is Entity entity)
                    {
                        // Clone and add to the new block record
                        Entity clonedEntity = (Entity)entity.Clone();
                        newBlockRecord.AppendEntity(clonedEntity);
                        tr.AddNewlyCreatedDBObject(clonedEntity, true);
                    }
                }

                // Create a new block reference for the updated block
                BlockReference newBlockRef = new BlockReference(Point3d.Origin, newBlockRecord.ObjectId);
                currentSpace.AppendEntity(newBlockRef);
                tr.AddNewlyCreatedDBObject(newBlockRef, true);

                // Remove the original block reference
                blockRef.UpgradeOpen(); // Ensure the block reference can be erased
                blockRef.Erase();

                tr.Commit();
            }
        }

        private bool IsHorizontal(Line line)
        {
            return line.StartPoint.Y == line.EndPoint.Y;
        }

        private double DistanceToLine(Point3d point, Line line)
        {
            // Calculate distance from point to the line segment
            double dx = line.EndPoint.X - line.StartPoint.X;
            double dy = line.EndPoint.Y - line.StartPoint.Y;
            double lengthSquared = dx * dx + dy * dy;
            if (lengthSquared == 0) return point.DistanceTo(line.StartPoint);

            double t = ((point.X - line.StartPoint.X) * dx + (point.Y - line.StartPoint.Y) * dy) / lengthSquared;
            t = Math.Max(0, Math.Min(1, t));
            Point3d projection = new Point3d(line.StartPoint.X + t * dx, line.StartPoint.Y + t * dy, 0);
            return point.DistanceTo(projection);
        }

        private double DistanceToSideOfLine(Point3d textPosition, Line line)
        {
            // Calculate the distance from the text to the side of the line
            if (IsHorizontal(line))
            {
                return Math.Abs(textPosition.Y - line.StartPoint.Y);
            }
            else
            {
                return Math.Abs(textPosition.X - line.StartPoint.X);
            }
        }*/


        /*[CommandMethod("RemoveIECTextFromBlock")]
        public void RemoveIECTextFromBlock()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // Prompt for selecting a block reference
                PromptEntityOptions blockOptions = new PromptEntityOptions("\nSelect a block reference: ");
                blockOptions.SetRejectMessage("\nOnly block references are allowed.");
                blockOptions.AddAllowedClass(typeof(BlockReference), true);
                PromptEntityResult blockResult = ed.GetEntity(blockOptions);

                if (blockResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\nCommand canceled.");
                    return;
                }

                // Get the block reference
                BlockReference blockRef = (BlockReference)tr.GetObject(blockResult.ObjectId, OpenMode.ForRead);

                // Access the block definition
                BlockTableRecord blockDef = (BlockTableRecord)tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForWrite);

                // Find the text object with the text "IEC"
                ObjectId textIdToRemove = ObjectId.Null;
                foreach (ObjectId objId in blockDef)
                {
                    DBObject obj = tr.GetObject(objId, OpenMode.ForWrite);
                    if (obj is DBText text && text.TextString == "IEC")
                    {
                        textIdToRemove = objId;
                        break;
                    }
                }

                // Remove the text object if found
                if (!textIdToRemove.IsNull)
                {
                    DBObject textToRemove = tr.GetObject(textIdToRemove, OpenMode.ForWrite);
                    textToRemove.Erase();
                    ed.WriteMessage("\nText 'IEC' removed from block.");
                }
                else
                {
                    ed.WriteMessage("\nText 'IEC' not found in block.");
                }

                // Commit the transaction
                tr.Commit();
            }

            // Regenerate the drawing to reflect the changes
            Application.DocumentManager.MdiActiveDocument.SendStringToExecute("REGEN\n", true, false, false);
            //Application.DocumentManager.MdiActiveDocument.SendStringToExecute("\n", true, false, false);

        }*/



        [CommandMethod("RemoveIECTextAndRelatedObjects")]
        public void RemoveIECTextAndRelatedObjects()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // Prompt for selecting a block reference
                PromptEntityOptions blockOptions = new PromptEntityOptions("\nSelect a block reference: ");
                blockOptions.SetRejectMessage("\nOnly block references are allowed.");
                blockOptions.AddAllowedClass(typeof(BlockReference), true);
                PromptEntityResult blockResult = ed.GetEntity(blockOptions);

                if (blockResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\nCommand canceled.");
                    return;
                }

                // Get the block reference and its block definition
                BlockReference blockRef = (BlockReference)tr.GetObject(blockResult.ObjectId, OpenMode.ForRead);
                BlockTableRecord blockDef = (BlockTableRecord)tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForWrite);

                // Initialize variables
                DBText iecText = null;
                Line closestLine = null;
                double minDistance = double.MaxValue;
                double removedLineY = double.NaN;

                // Find the "IEC" text and the closest horizontal line
                foreach (ObjectId objId in blockDef)
                {
                    DBObject obj = tr.GetObject(objId, OpenMode.ForWrite);
                    if (obj is DBText text && text.TextString == "IEC")
                    {
                        iecText = text;
                        // Find the closest horizontal line to this text
                        foreach (ObjectId innerObjId in blockDef)
                        {
                            DBObject innerObj = tr.GetObject(innerObjId, OpenMode.ForWrite);
                            if (innerObj is Line line && IsHorizontal(line))
                            {
                                double distance = DistanceToLine(text.Position, line);
                                if (distance < minDistance)
                                {
                                    minDistance = distance;
                                    closestLine = line;
                                }
                            }
                        }
                        break; // We only need to find one "IEC" text
                    }
                }

                // Remove the IEC text
                if (iecText != null)
                {
                    iecText.Erase();
                    ed.WriteMessage("\nText 'IEC' removed from block.");
                }
                else
                {
                    ed.WriteMessage("\nText 'IEC' not found in block.");
                }

                // Remove the closest horizontal line if found and record its Y coordinate
                if (closestLine != null)
                {
                    removedLineY = closestLine.StartPoint.Y;
                    closestLine.Erase();
                    ed.WriteMessage("\nClosest horizontal line removed from block.");
                }

                // Find and remove the single closest text to the left or right of the removed line
                DBText closestText = null;
                double minSideDistance = double.MaxValue;

                foreach (ObjectId objId in blockDef)
                {
                    DBObject obj = tr.GetObject(objId, OpenMode.ForWrite);
                    if (obj is DBText text && text != iecText)
                    {
                        double distance = DistanceToSideOfLine(text.Position, closestLine);
                        if (distance < minSideDistance)
                        {
                            minSideDistance = distance;
                            closestText = text;
                        }
                    }
                }

                // Remove the closest text if found
                if (closestText != null)
                {
                    closestText.Erase();
                    ed.WriteMessage("\nClosest text to the right or left of the removed line removed.");
                }

                // Find the bottommost horizontal line
                Line bottommostLine = null;
                double minY = double.MaxValue;

                foreach (ObjectId objId in blockDef)
                {
                    DBObject obj = tr.GetObject(objId, OpenMode.ForWrite);
                    if (obj is Line line && IsHorizontal(line))
                    {
                        if (line.StartPoint.Y < minY)
                        {
                            minY = line.StartPoint.Y;
                            bottommostLine = line;
                        }
                    }
                }

                // Calculate the distance difference
                double distanceDifference = removedLineY - minY;

                // Move all objects below the removed line up by the distance difference
                foreach (ObjectId objId in blockDef)
                {
                    DBObject obj = tr.GetObject(objId, OpenMode.ForWrite);
                    if (obj is Entity entity)
                    {
                        if (entity.Bounds != null && entity.Bounds.Value.MinPoint.Y < removedLineY)
                        {
                            entity.TransformBy(Matrix3d.Displacement(new Vector3d(0, distanceDifference, 0)));
                        }
                    }
                }

                // Move all text boxes below the removed line up by the distance difference
                foreach (ObjectId objId in blockDef)
                {
                    DBObject obj = tr.GetObject(objId, OpenMode.ForWrite);
                    if (obj is DBText text && text.Position.Y < removedLineY)
                    {
                        text.Position = new Point3d(text.Position.X, text.Position.Y + distanceDifference, text.Position.Z);
                    }
                }

                // Cut off the top end of the moved vertical lines by the distance difference
                foreach (ObjectId objId in blockDef)
                {
                    DBObject obj = tr.GetObject(objId, OpenMode.ForWrite);
                    if (obj is Line line && IsVertical(line))
                    {
                        if (line.StartPoint.Y < removedLineY && line.EndPoint.Y > removedLineY)
                        {
                            line.EndPoint = new Point3d(line.EndPoint.X, line.EndPoint.Y - distanceDifference, line.EndPoint.Z);
                        }
                        else if (line.EndPoint.Y < removedLineY && line.StartPoint.Y > removedLineY)
                        {
                            line.StartPoint = new Point3d(line.StartPoint.X, line.StartPoint.Y - distanceDifference, line.StartPoint.Z);
                        }
                    }
                }

                // Commit the transaction
                tr.Commit();
            }

            // Regenerate the drawing to reflect the changes
            Application.DocumentManager.MdiActiveDocument.SendStringToExecute("REGEN\n", true, false, false);
        }

        private bool IsHorizontal(Line line)
        {
            return line.StartPoint.Y == line.EndPoint.Y;
        }

        private bool IsVertical(Line line)
        {
            return line.StartPoint.X == line.EndPoint.X;
        }

        private double DistanceToLine(Point3d point, Line line)
        {
            // Calculate distance from point to the line segment
            double dx = line.EndPoint.X - line.StartPoint.X;
            double dy = line.EndPoint.Y - line.StartPoint.Y;
            double lengthSquared = dx * dx + dy * dy;
            if (lengthSquared == 0) return point.DistanceTo(line.StartPoint);

            double t = ((point.X - line.StartPoint.X) * dx + (point.Y - line.StartPoint.Y) * dy) / lengthSquared;
            t = Math.Max(0, Math.Min(1, t));
            Point3d projection = new Point3d(line.StartPoint.X + t * dx, line.StartPoint.Y + t * dy, 0);
            return point.DistanceTo(projection);
        }

        private double DistanceToSideOfLine(Point3d textPosition, Line line)
        {
            // Calculate the distance from the text to the side of the line
            if (IsHorizontal(line))
            {
                return Math.Abs(textPosition.Y - line.StartPoint.Y);
            }
            else
            {
                return Math.Abs(textPosition.X - line.StartPoint.X);
            }
        }














        [CommandMethod("DrawSignalArrow")]
        public void DrawSignalArrow()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            // Prompt for line or polyline selection
            PromptEntityOptions options = new PromptEntityOptions("\nSelect a line or polyline:");
            options.SetRejectMessage("\nMust be a line or polyline.");
            options.AddAllowedClass(typeof(Line), true);
            options.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult result = ed.GetEntity(options);

            if (result.Status != PromptStatus.OK) return;

            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                Entity entity = trans.GetObject(result.ObjectId, OpenMode.ForRead) as Entity;

                // Determine if entity is Line or Polyline
                if (entity is Line line)
                {
                    DrawArrowOnLine(trans, line);
                }
                else if (entity is Polyline polyline)
                {
                    DrawArrowOnPolyline(trans, polyline);
                }

                // Commit the transaction
                trans.Commit();
            }
        }

        private void DrawArrowOnLine(Transaction trans, Line line)
        {
            // Prompt for input endpoint
            Point3d inputPoint = GetEndpoint("Select the input endpoint:", line);
            if (inputPoint == Point3d.Origin) return; // User canceled

            // Prompt for output endpoint
            Point3d outputPoint = GetEndpoint("Select the output endpoint:", line);
            if (outputPoint == Point3d.Origin) return; // User canceled

            // Calculate the midpoint of the line
            Point3d midpoint = new Point3d((line.StartPoint.X + line.EndPoint.X) / 2,
                                             (line.StartPoint.Y + line.EndPoint.Y) / 2,
                                             (line.StartPoint.Z + line.EndPoint.Z) / 2);

            // Calculate the direction from input to output
            Vector3d direction = outputPoint - inputPoint;
            DrawArrow(trans, midpoint, direction, line); // In DrawArrowOnLine

        }

        private void DrawArrowOnPolyline(Transaction trans, Polyline polyline)
        {
            // Prompt for input endpoint
            Point3d inputPoint = GetEndpoint("Select the input endpoint:", polyline);
            if (inputPoint == Point3d.Origin) return; // User canceled

            // Prompt for output endpoint
            Point3d outputPoint = GetEndpoint("Select the output endpoint:", polyline);
            if (outputPoint == Point3d.Origin) return; // User canceled

            // Find the total length of the polyline and calculate the midpoint
            double totalLength = 0.0;
            for (int i = 0; i < polyline.NumberOfVertices - 1; i++)
            {
                totalLength += polyline.GetPoint3dAt(i).DistanceTo(polyline.GetPoint3dAt(i + 1));
            }

            // Now find the point at half the total length
            double halfLength = totalLength / 2.0;
            double accumulatedLength = 0.0;
            Point3d leftpoint = polyline.GetPoint3dAt(0);
            Point3d rightpoint = polyline.GetPoint3dAt(0);
            Point3d midpoint = polyline.GetPoint3dAt(0);
            Vector3d direction = outputPoint - inputPoint;

            for (int i = 0; i < polyline.NumberOfVertices - 1; i++)
            {
                double segmentLength = polyline.GetPoint3dAt(i).DistanceTo(polyline.GetPoint3dAt(i + 1));
                if (accumulatedLength + segmentLength >= halfLength)
                {
                    double t = (halfLength - accumulatedLength) / segmentLength;
                    midpoint = polyline.GetPoint3dAt(i).Add(
                        (polyline.GetPoint3dAt(i + 1) - polyline.GetPoint3dAt(i)).GetNormal() * (t * segmentLength));
                    leftpoint = polyline.GetPoint3dAt(i);
                    rightpoint = polyline.GetPoint3dAt(i + 1);
                    break;
                }
                accumulatedLength += segmentLength;
            }

            // Calculate the direction from input to output
            Point3d halfwayPoint = polyline.GetPoint3dAt(0);
            if (polyline.GetPoint3dAt(0) == inputPoint)
            {
                direction = rightpoint - leftpoint;
                halfwayPoint = leftpoint + direction * 0.5;
            }
            else
            {
                direction = leftpoint - rightpoint;
                halfwayPoint = rightpoint + direction * 0.5;

            }



            DrawArrow(trans, halfwayPoint, direction, polyline);
        }

        private void DrawArrow(Transaction trans, Point3d position, Vector3d direction, Entity entity)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            BlockTableRecord btr;

            using (BlockTable blockTable = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForRead))
            {
                btr = (BlockTableRecord)trans.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            }

            // Normalize direction
            direction = direction.GetNormal();
            double arrowLength = 1; // Length for the arrow
            double arrowWidth = 1.0; // Adjust width as needed

            // Calculate the arrow tip based on the position
            Point3d arrowTip = position;
            double triangleangle = Math.PI / 6;

            // Create arrowhead points
            Point3d arrowBase1 = arrowTip - direction.RotateBy(triangleangle, Vector3d.ZAxis) * arrowLength;
            Point3d arrowBase2 = arrowTip - direction.RotateBy(-triangleangle, Vector3d.ZAxis) * arrowLength;

            arrowTip = arrowTip + direction * arrowLength * Math.Cos(triangleangle);
            arrowBase1 = arrowBase1 + direction * arrowLength * Math.Cos(triangleangle);
            arrowBase2 = arrowBase2 + direction * arrowLength * Math.Cos(triangleangle);

            // Create arrow shape
            using (Polyline arrow = new Polyline(3))
            {
                arrow.AddVertexAt(0, new Point2d(arrowTip.X, arrowTip.Y), 0, 0, 0);
                arrow.AddVertexAt(1, new Point2d(arrowBase1.X, arrowBase1.Y), 0, 0, 0);
                arrow.AddVertexAt(2, new Point2d(arrowBase2.X, arrowBase2.Y), 0, 0, 0);
                arrow.Closed = true;

                btr.AppendEntity(arrow);
                arrow.Layer = "0"; // Set the layer for the arrow as needed

                // Join arrow to the original entity
                if (entity != null)
                {
                    // Make the original entity writable
                    entity.UpgradeOpen();
                    // Attempt to join the arrow to the original entity
                    if (entity is Polyline polyline)
                    {
                        polyline.UpgradeOpen();
                        polyline.JoinEntity(arrow);
                    }
                    else if (entity is Line line)
                    {
                        // Convert the line to a polyline to join
                        Polyline linePolyline = new Polyline(2);
                        linePolyline.AddVertexAt(0, new Point2d(line.StartPoint.X, line.StartPoint.Y), 0, 0, 0);
                        linePolyline.AddVertexAt(1, new Point2d(line.EndPoint.X, line.EndPoint.Y), 0, 0, 0);
                        linePolyline.Closed = false;
                        btr.AppendEntity(linePolyline);
                        // Join the arrow to the new polyline
                        linePolyline.JoinEntity(arrow);
                    }
                }
            }
        }


        private Point3d GetEndpoint(string message, Entity entity)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            PromptPointOptions options = new PromptPointOptions(message);
            options.AllowNone = false;
            options.UseBasePoint = true;

            // Default to the start point of the entity
            if (entity is Line line)
            {
                options.BasePoint = line.StartPoint;
            }
            else if (entity is Polyline polyline)
            {
                options.BasePoint = polyline.GetPoint3dAt(0); // Start point of polyline
            }

            PromptPointResult result = ed.GetPoint(options);
            if (result.Status != PromptStatus.OK) return Point3d.Origin;

            return result.Value;
        }





        /*

        [CommandMethod("DrawSignalArrow")]
        public void DrawSignalArrow()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            // Prompt for line or polyline selection
            PromptEntityOptions options = new PromptEntityOptions("\nSelect a line or polyline:");
            options.SetRejectMessage("\nMust be a line or polyline.");
            options.AddAllowedClass(typeof(Line), true);
            options.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult result = ed.GetEntity(options);

            if (result.Status != PromptStatus.OK) return;

            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                Entity entity = trans.GetObject(result.ObjectId, OpenMode.ForRead) as Entity;

                // Determine if entity is Line or Polyline
                if (entity is Line line)
                {
                    DrawArrowOnLine(trans, line);
                }
                else if (entity is Polyline polyline)
                {
                    DrawArrowOnPolyline(trans, polyline);
                }

                // Commit the transaction
                trans.Commit();
            }
        }

        private void DrawArrowOnLine(Transaction trans, Line line)
        {
            // Prompt for input endpoint
            Point3d inputPoint = GetEndpoint("Select the input endpoint:", line);
            if (inputPoint == Point3d.Origin) return; // User canceled

            // Prompt for output endpoint
            Point3d outputPoint = GetEndpoint("Select the output endpoint:", line);
            if (outputPoint == Point3d.Origin) return; // User canceled

            // Calculate the direction from input to output
            Vector3d direction = outputPoint - inputPoint;

            // Create new polyline to include the original line and arrow
            using (Polyline polyline = new Polyline())
            {
                polyline.AddVertexAt(0, new Point2d(line.StartPoint.X, line.StartPoint.Y), 0, 0, 0);
                polyline.AddVertexAt(1, new Point2d(line.EndPoint.X, line.EndPoint.Y), 0, 0, 0);

                DrawArrow(trans, polyline, inputPoint, direction);

                // Add polyline to the drawing
                AddPolylineToDatabase(trans, polyline);
            }
        }

        private void DrawArrowOnPolyline(Transaction trans, Polyline polyline)
        {
            // Prompt for input endpoint
            Point3d inputPoint = GetEndpoint("Select the input endpoint:", polyline);
            if (inputPoint == Point3d.Origin) return; // User canceled

            // Prompt for output endpoint
            Point3d outputPoint = GetEndpoint("Select the output endpoint:", polyline);
            if (outputPoint == Point3d.Origin) return; // User canceled

            // Calculate the direction from input to output
            Vector3d direction = inputPoint - outputPoint;
            Point3d leftpoint = polyline.GetPoint3dAt(0);
            Point3d rightpoint = polyline.GetPoint3dAt(0);
            // Create new polyline to include the original polyline and arrow
            using (Polyline newPolyline = new Polyline())
            {
                if (outputPoint == polyline.GetPoint3dAt(0))
                {

                    for (int i = 0; i < polyline.NumberOfVertices; i++)
                    {
                        Point3d pt = polyline.GetPoint3dAt(i);
                        leftpoint = polyline.GetPoint3dAt(i);
                        if (i != 0)
                        {
                            rightpoint = polyline.GetPoint3dAt(i - 1);
                        }
                        newPolyline.AddVertexAt(i, new Point2d(pt.X, pt.Y), 0, 0, 0);


                    }
                }

                else
                {

                    for (int i = polyline.NumberOfVertices - 1; i >= 0; i--)
                    {
                        Point3d pt = polyline.GetPoint3dAt(i);
                        leftpoint = polyline.GetPoint3dAt(i);
                        if (i != polyline.NumberOfVertices - 1)
                        {
                            rightpoint = polyline.GetPoint3dAt(i + 1);
                        }
                        newPolyline.AddVertexAt(polyline.NumberOfVertices - 1 - i, new Point2d(pt.X, pt.Y), 0, 0, 0);


                    }
                }


                direction = leftpoint - rightpoint;

                

                DrawArrow(trans, newPolyline, inputPoint, direction);

                // Add new polyline to the drawing
                AddPolylineToDatabase(trans, newPolyline);
            }
        } 

        



        private void DrawArrow(Transaction trans, Polyline polyline, Point3d position, Vector3d direction)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            double arrowLength = 1.0; // Length of the arrow
            double triangleAngle = Math.PI / 6;

            // Arrow tip at the input position
            Point3d arrowTip = position;

            // Direction for the base points of the arrow (backwards)
            Vector3d baseDirection = direction.GetNormal() * -1;

            // Create arrowhead points
            Point3d arrowBase1 = arrowTip + baseDirection.RotateBy(triangleAngle, Vector3d.ZAxis) * arrowLength;
            Point3d arrowBase2 = arrowTip + baseDirection.RotateBy(-triangleAngle, Vector3d.ZAxis) * arrowLength;

            // Add arrowhead as part of the same polyline
            int arrowVertexIndex = polyline.NumberOfVertices;
            polyline.AddVertexAt(arrowVertexIndex, new Point2d(arrowTip.X, arrowTip.Y), 0, 0, 0);
            polyline.AddVertexAt(arrowVertexIndex + 1, new Point2d(arrowBase1.X, arrowBase1.Y), 0, 0, 0);
            polyline.AddVertexAt(arrowVertexIndex + 2, new Point2d(arrowBase2.X, arrowBase2.Y), 0, 0, 0);
            polyline.AddVertexAt(arrowVertexIndex + 3, new Point2d(arrowTip.X, arrowTip.Y), 0, 0, 0); // Back to tip

            polyline.Closed = false; // Keep it open
        }

        private void AddPolylineToDatabase(Transaction trans, Polyline polyline)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            BlockTableRecord btr;

            using (BlockTable blockTable = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForRead))
            {
                btr = (BlockTableRecord)trans.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                btr.AppendEntity(polyline);
            }
        }

        private Point3d GetEndpoint(string message, Entity entity)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            PromptPointOptions options = new PromptPointOptions(message);
            options.AllowNone = false;
            options.UseBasePoint = true;

            // Default to the start point of the entity
            if (entity is Line line)
            {
                options.BasePoint = line.StartPoint;
            }
            else if (entity is Polyline polyline)
            {
                options.BasePoint = polyline.GetPoint3dAt(0); // Start point of polyline
            }

            PromptPointResult result = ed.GetPoint(options);
            if (result.Status != PromptStatus.OK) return Point3d.Origin;

            return result.Value;
        }




        */

        /*[CommandMethod("CreateArrow")]
        public void CreateArrow()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            // Prompt user to select a polyline
            PromptEntityOptions peo = new PromptEntityOptions("\nSelect a polyline: ");
            peo.SetRejectMessage("\nEntity must be a polyline.");
            peo.AddAllowedClass(typeof(Polyline), true);

            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK)
                return;

            ObjectId polylineId = per.ObjectId;

            // Prompt user to select an endpoint of the polyline
            PromptPointOptions ppo = new PromptPointOptions("\nSelect an endpoint of the polyline: ");
            PromptPointResult ppr = ed.GetPoint(ppo);
            if (ppr.Status != PromptStatus.OK)
                return;

            Point3d selectedPoint = ppr.Value;

            // Start a transaction to manipulate the polyline
            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                Polyline polyline = tr.GetObject(polylineId, OpenMode.ForRead) as Polyline;

                if (polyline == null)
                {
                    ed.WriteMessage("\nSelected entity is not a polyline.");
                    return;
                }

                // Determine the direction of the polyline from the selected endpoint
                int vertexIndex = -1;
                for (int i = 0; i < polyline.NumberOfVertices; i++)
                {
                    Point3d vertex = polyline.GetPoint3dAt(i);
                    if (vertex.IsEqualTo(selectedPoint))
                    {
                        vertexIndex = i;
                        break;
                    }
                }

                if (vertexIndex == -1)
                {
                    ed.WriteMessage("\nSelected point is not a vertex of the polyline.");
                    return;
                }

                // Get the next vertex in the polyline to determine the direction
                Point3d nextVertex;
                if (vertexIndex == polyline.NumberOfVertices - 1)
                {
                    nextVertex = polyline.GetPoint3dAt(0); // For closed polyline
                }
                else
                {
                    nextVertex = polyline.GetPoint3dAt(vertexIndex + 1);
                }

                // Compute the direction vector from the selected endpoint to the next vertex
                Vector3d direction = nextVertex - selectedPoint;
                direction = direction.GetNormal();  // Normalize the direction vector

                // Define the arrow length and width
                double arrowLength = 1.0;  // You can adjust this
                double arrowWidth = 1.0;    // This controls the width of the arrowhead

                // Create the arrow polyline
                Polyline arrow = new Polyline();
                arrow.AddVertexAt(0, new Point2d(selectedPoint.X, selectedPoint.Y), 0, 0, arrowWidth); // Start point
                arrow.AddVertexAt(1, new Point2d(selectedPoint.X + direction.X * arrowLength, selectedPoint.Y + direction.Y * arrowLength), 0, 0, arrowWidth); // End point

                // Add the arrow polyline to the database
                BlockTableRecord btr = tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;
                btr.AppendEntity(arrow);
                tr.AddNewlyCreatedDBObject(arrow, true);

                // Join the newly created arrow with the original polyline
                polyline.UpgradeOpen();
                polyline.JoinEntity(arrow);

                // Commit the transaction
                tr.Commit();
            }

            ed.WriteMessage("\nArrow created and polyline joined successfully.");
        }
        first create arrow, incorrect direction using other vertex
    */





        /*[CommandMethod("CreateArrow")]
        public void CreateArrow()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            // Prompt user to select a polyline
            PromptEntityOptions peo = new PromptEntityOptions("\nSelect a polyline: ");
            peo.SetRejectMessage("\nEntity must be a polyline.");
            peo.AddAllowedClass(typeof(Polyline), true);

            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK)
                return;

            ObjectId polylineId = per.ObjectId;

            // Prompt user to select an endpoint of the polyline
            PromptPointOptions ppo = new PromptPointOptions("\nSelect an endpoint of the polyline: ");
            PromptPointResult ppr = ed.GetPoint(ppo);
            if (ppr.Status != PromptStatus.OK)
                return;

            Point3d selectedPoint = ppr.Value;

            // Start a transaction to manipulate the polyline
            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                Polyline polyline = tr.GetObject(polylineId, OpenMode.ForRead) as Polyline;

                if (polyline == null)
                {
                    ed.WriteMessage("\nSelected entity is not a polyline.");
                    return;
                }

                // Find the index of the selected point on the polyline
                int vertexIndex = -1;
                for (int i = 0; i < polyline.NumberOfVertices; i++)
                {
                    Point3d vertex = polyline.GetPoint3dAt(i);
                    if (vertex.IsEqualTo(selectedPoint))
                    {
                        vertexIndex = i;
                        break;
                    }
                }

                if (vertexIndex == -1)
                {
                    ed.WriteMessage("\nSelected point is not a vertex of the polyline.");
                    return;
                }

                // Use a small step to move along the polyline and calculate the direction vector
                double stepDistance = -1; // Small step to calculate direction

                // Get the point at the next step along the polyline from the selected point
                double paramAtSelectedPoint = polyline.GetParameterAtPoint(selectedPoint); // Parameter at selected point
                if (paramAtSelectedPoint == 0)
                {
                    stepDistance = 1;
                }
                double paramNextPoint = paramAtSelectedPoint + stepDistance;

                /*if (paramNextPoint > polyline.Length)
                {
                    // If the step goes past the end of the polyline, we loop around (for closed polylines)
                    paramNextPoint = paramNextPoint - polyline.Length;
                }

                // Get the next point at the small step distance along the polyline
                Point3d nextPoint = polyline.GetPointAtParameter(paramNextPoint);

                // Compute the direction vector
                Vector3d direction = nextPoint - selectedPoint;
                direction = direction.GetNormal();  // Normalize the direction vector

                // Define the arrow length and width
                double arrowLength = 1.0;  // You can adjust this
                double arrowWidth = 1.0;    // This controls the width of the arrowhead

                // Create the arrow polyline
                Polyline arrow = new Polyline();
                arrow.AddVertexAt(0, new Point2d(selectedPoint.X, selectedPoint.Y), 0, 0, arrowWidth); // Start point
                arrow.AddVertexAt(1, new Point2d(selectedPoint.X + direction.X * arrowLength, selectedPoint.Y + direction.Y * arrowLength), 0, 0, arrowWidth); // End point

                // Add the arrow polyline to the database
                BlockTableRecord btr = tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;
                btr.AppendEntity(arrow);
                tr.AddNewlyCreatedDBObject(arrow, true);

                // Join the newly created arrow with the original polyline
                polyline.UpgradeOpen();
                try
                {
                    polyline.JoinEntity(arrow);
                }

                catch (Autodesk.AutoCAD.Runtime.Exception ex)
                {
                ed.WriteMessage($"\nERROR: Can only connect arrow to endpoints of line. Endpoint was not selected. Arrow was not joined with line.");

            }


                // Commit the transaction
                tr.Commit();
            //ed.WriteMessage($"\nArrow created and polyline joined successfully. paramnextpoint: {paramNextPoint}  paramAtSelectedPoint: {paramAtSelectedPoint} selectedPoint {selectedPoint} ");
        }

            ed.WriteMessage($"\nArrow created and polyline joined successfully." );
        }

    correct code for single line

    */


        [CommandMethod("CreateArrows")]
        public void CreateArrows()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            // Prompt user to select multiple polylines
            PromptSelectionOptions selectionOptions = new PromptSelectionOptions
            {
                MessageForAdding = "\nSelect multiple polylines: ",
                AllowDuplicates = false
            };

            PromptSelectionResult selectionResult = ed.GetSelection(selectionOptions);
            if (selectionResult.Status != PromptStatus.OK)
            {
                return; // Exit if no selection was made
            }

            // Start a transaction to manipulate the polylines
            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                // Iterate through each selected polyline
                foreach (ObjectId objId in selectionResult.Value.GetObjectIds())
                {
                    // Open the polyline
                    Polyline polyline = tr.GetObject(objId, OpenMode.ForRead) as Polyline;
                    if (polyline == null)
                    {
                        continue; // Skip if not a polyline
                    }

                    // Prompt user to select an endpoint of the current polyline
                    PromptPointOptions ppo = new PromptPointOptions("\nSelect an endpoint of the polyline: ");
                    PromptPointResult ppr = ed.GetPoint(ppo);
                    if (ppr.Status != PromptStatus.OK)
                        continue;

                    Point3d selectedPoint = ppr.Value;

                    // Ensure the selected point is a valid endpoint (vertex)
                    int vertexIndex = -1;
                    for (int i = 0; i < polyline.NumberOfVertices; i++)
                    {
                        Point3d vertex = polyline.GetPoint3dAt(i);
                        if (vertex.IsEqualTo(selectedPoint))
                        {
                            vertexIndex = i;
                            break;
                        }
                    }

                    if (vertexIndex == -1)
                    {
                        ed.WriteMessage("\nSelected point is not a vertex of the polyline.");
                        continue; // Skip if the point is not a vertex
                    }

                    // Use a small step to move along the polyline and calculate the direction vector
                    double stepDistance = 1.0; // Small step to calculate direction

                    // Get the parameter of the selected point
                    double paramAtSelectedPoint = polyline.GetParameterAtPoint(selectedPoint);
                    double paramNextPoint = paramAtSelectedPoint + stepDistance;

                    // Get the next point at the small step distance along the polyline
                    Point3d nextPoint = polyline.GetPointAtParameter(paramNextPoint);

                    // Compute the direction vector
                    Vector3d direction = nextPoint - selectedPoint;
                    direction = direction.GetNormal();  // Normalize the direction vector

                    // Define the arrow length and width
                    double arrowLength = 1.0;  // Adjust this as needed
                    double arrowWidth = 1.0;   // Adjust this as needed

                    // Create the arrow polyline
                    Polyline arrow = new Polyline();
                    arrow.AddVertexAt(0, new Point2d(selectedPoint.X, selectedPoint.Y), 0, 0, arrowWidth); // Start point
                    arrow.AddVertexAt(1, new Point2d(selectedPoint.X + direction.X * arrowLength, selectedPoint.Y + direction.Y * arrowLength), 0, 0, arrowWidth); // End point

                    // Add the arrow polyline to the database
                    BlockTableRecord btr = tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;
                    btr.AppendEntity(arrow);
                    tr.AddNewlyCreatedDBObject(arrow, true);

                    // Join the newly created arrow with the original polyline
                    polyline.UpgradeOpen();
                    try
                    {
                        polyline.JoinEntity(arrow);  // Join the arrow to the polyline
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        ed.WriteMessage($"\nERROR: Can only connect arrow to endpoints of line. Endpoint was not selected. Arrow was not joined with line.");
                    }
                }

                // Commit the transaction
                tr.Commit();
            }

            ed.WriteMessage($"\nArrows created and polylines joined successfully.");
        }












    }
}
