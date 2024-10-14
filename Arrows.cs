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
    public class Arrows
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

        private SelectionSet PromptFunc(Editor ed)
        {
            SelectionSet selectedEntities = null;
            // Check if there is a previously selected set of entities
            PromptSelectionResult selResult = ed.SelectImplied();

            if (selResult.Status == PromptStatus.OK)
            {
                // If there is a previous selection, we use that
                selectedEntities = selResult.Value;
                ed.WriteMessage("Number of objects selected: " + selectedEntities.Count.ToString());
            }

            else
            {
                // Clear the PickFirst selection set
                ObjectId[] idarrayEmpty = new ObjectId[0];
                ed.SetImpliedSelection(idarrayEmpty);
                ed.WriteMessage("No Previous Objects Selected");
                selResult = ed.GetSelection();
                // Prompt user to select multiple polylines
                PromptSelectionOptions selOptions = new PromptSelectionOptions();
                selOptions.MessageForAdding = "\nSelect polylines: ";

                if (selResult.Status == PromptStatus.OK)
                {
                    selectedEntities = selResult.Value;

                    ed.WriteMessage("Number of objects selected: " + selectedEntities.Count.ToString());

                }
                else
                {
                    Application.ShowAlertDialog("Number of objects selected: 0");

                }
            }
            return selectedEntities;
        }

        private Polyline MakeArrowHead(Point3d v1, Point3d v2)
        {
            Polyline arrowhead = new Polyline();
            // Compute the direction vector
            Vector3d direction = v2 - v1;
            direction = direction.GetNormal();  // Normalize the direction vector

            // Define the arrow length and width
            double arrowLength = 1.0;  // You can adjust this
            double arrowWidth = 1.0;   // This controls the width of the arrowhead

            // Create the arrow polyline (this will be part of the original polyline)
            arrowhead.AddVertexAt(0, new Point2d(v1.X, v1.Y), 0, 0, arrowWidth); // Start point
            arrowhead.AddVertexAt(1, new Point2d(v1.X + direction.X * arrowLength, v1.Y + direction.Y * arrowLength), 0, 0, arrowWidth); // End point
            return arrowhead;
        }

        [CommandMethod("Arrow", CommandFlags.UsePickSet)]
        public void Arrow()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            SelectionSet selectedEntities = PromptFunc(ed);

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                BlockTableRecord btr = tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;

                foreach (SelectedObject selectedObj in selectedEntities)
                {
                    // Check if the entity is a polyline
                    Polyline polyline = tr.GetObject(selectedObj.ObjectId, OpenMode.ForRead) as Polyline;
                    if (polyline == null)
                    {
                        ed.WriteMessage("\nSelected entity is not a polyline.");
                        continue;
                    }
                    
                    // Find the last vertex of the polyline
                    Point3d lastVertex = polyline.GetPoint3dAt(polyline.NumberOfVertices - 1);
                    Point3d seclastVertex = polyline.GetPoint3dAt(polyline.NumberOfVertices - 2);
                    Polyline arrow = MakeArrowHead(lastVertex, seclastVertex);

                    // Add the arrow polyline to the current space but don't append it independently
                    try
                    {
                        // Now, add the arrow to the polyline directly
                        polyline.UpgradeOpen();
                        polyline.JoinEntity(arrow);
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        ed.WriteMessage($"\nERROR: Could not join arrow with polyline. {ex.Message}");
                        continue;
                    }
                }

                // Commit the transaction
                tr.Commit();
            }

            ed.WriteMessage($"\nArrows created for all selected polylines.");
        }

        [CommandMethod("BackwardsArrow", CommandFlags.UsePickSet)]
        public void BackwardsArrow()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            SelectionSet selectedEntities = PromptFunc(ed);

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                BlockTableRecord btr = tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;

                foreach (SelectedObject selectedObj in selectedEntities)
                {
                    // Check if the entity is a polyline
                    Polyline polyline = tr.GetObject(selectedObj.ObjectId, OpenMode.ForRead) as Polyline;
                    if (polyline == null)
                    {
                        ed.WriteMessage("\nSelected entity is not a polyline.");
                        continue;
                    }

                    // Find the first vertex of the polyline
                    Point3d firstvertex = polyline.GetPoint3dAt(0);
                    Point3d secondVertex = polyline.GetPoint3dAt(1);
                    Polyline arrow = MakeArrowHead(firstvertex, secondVertex);


                    // Add the arrow polyline to the current space but don't append it independently
                    try
                    {
                        // Now, add the arrow to the polyline directly
                        polyline.UpgradeOpen();
                        polyline.JoinEntity(arrow);
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        ed.WriteMessage($"\nERROR: Could not join arrow with polyline. {ex.Message}");
                        continue;
                    }
                }

                // Commit the transaction
                tr.Commit();
            }

            ed.WriteMessage($"\nArrows created for all selected polylines.");
        }





        [CommandMethod("SwitchArrow", CommandFlags.UsePickSet)]
        public void SwitchArrow()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            SelectionSet selectedEntities = PromptFunc(ed);

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                BlockTableRecord btr = tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;
                bool isBackwards = false;
                foreach (SelectedObject selectedObj in selectedEntities)
                {
                    // Check if the entity is a polyline
                    Polyline polyline = tr.GetObject(selectedObj.ObjectId, OpenMode.ForRead) as Polyline;
                    if (polyline == null)
                    {
                        ed.WriteMessage("\nSelected entity is not a polyline.");
                        continue;
                    }

                    // If polyline has less than 2 vertices, skip
                    if (polyline.NumberOfVertices < 2)
                    {
                        ed.WriteMessage("\nPolyline has less than two vertices, cannot reverse direction.");
                        continue;
                    }

                    // Upgrade the polyline to be modified
                    polyline.UpgradeOpen();
                    if (polyline.GetStartWidthAt(0) > 0)
                    {
                        polyline.RemoveVertexAt(0);
                        isBackwards = true;
                    }
                    else
                    {
                        polyline.RemoveVertexAt(polyline.NumberOfVertices - 1);
                    }

                    polyline.ReverseCurve();
                    Polyline arrow;
                    if (isBackwards)
                    {
                        // Find the first vertex of the polyline

                        arrow = MakeArrowHead(polyline.GetPoint3dAt(0), polyline.GetPoint3dAt(1));

                    }
                    else
                    {
                        // Find the last vertex of the polyline
                        Point3d lastVertex = polyline.GetPoint3dAt(polyline.NumberOfVertices - 1);
                        Point3d seclastVertex = polyline.GetPoint3dAt(polyline.NumberOfVertices - 2);
                        arrow = MakeArrowHead(lastVertex, seclastVertex);

                        // Add the arrow polyline to the current space but don't append it independently
                    }


                    // Add the arrow polyline to the current space but don't append it independently
                    try
                    {
                        // Now, add the arrow to the polyline directly
                        polyline.JoinEntity(arrow);
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        ed.WriteMessage($"\nERROR: Could not join arrow with polyline. {ex.Message}");
                        continue;
                    }
                }

                // Commit the transaction
                tr.Commit();
            }

            ed.WriteMessage($"\nArrow direction switched for all selected polylines.");
        }

    }
}
