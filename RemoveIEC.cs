using System;
namespace PluginCommands
{
	public class RemoveIEC
	{
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

    }
}