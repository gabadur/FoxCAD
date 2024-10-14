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
public class Aligning
{

    [CommandMethod("PS_AlignObjects", CommandFlags.UsePickSet)]
    public void AlignObjects()
    {
        var document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
        var editor = document.Editor;
        SelectionSet selectedEntities;
        PromptSelectionResult selResult = editor.SelectImplied();

        if (selResult.Status == PromptStatus.OK)
        {
            // If there is a previous selection, we use that
            selectedEntities = selResult.Value;
            editor.WriteMessage("Number of objects selected: " + selectedEntities.Count.ToString());
        }

        else
        {
            // Clear the PickFirst selection set
            ObjectId[] idarrayEmpty = new ObjectId[0];
            editor.SetImpliedSelection(idarrayEmpty);
            editor.WriteMessage("No Previous Objects Selected");
            selResult = editor.GetSelection();
            // Prompt user to select multiple polylines
            PromptSelectionOptions selOptions = new PromptSelectionOptions();
            selOptions.MessageForAdding = "\nSelect polylines: ";

            if (selResult.Status == PromptStatus.OK)
            {
                selectedEntities = selResult.Value;

                editor.WriteMessage("Number of objects selected: " + selectedEntities.Count.ToString());

            }
            else
            {
                Application.ShowAlertDialog("Number of objects selected: 0");

                return;
            }
        }


        // Get the selected objects

        ObjectId[] objectIds = selectedEntities.GetObjectIds();

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
}
}
