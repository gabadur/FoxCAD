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
    public class Feathers
    {
        [CommandMethod("Feather", CommandFlags.UsePickSet)]
        public void Feather()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            SelectionSet selectedEntities = PromptFunc(ed);
            if (selectedEntities != null)
            {
                for (int i = selectedEntities.Count - 1; i >= 0; i--)
                {
                    SelectedObject selectedObj = selectedEntities[i];
                    using (Transaction tr = doc.TransactionManager.StartTransaction())
                    {
                        BlockTableRecord btr = tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;
                        Polyline polyline = tr.GetObject(selectedObj.ObjectId, OpenMode.ForRead) as Polyline;
                        Line line = tr.GetObject(selectedObj.ObjectId, OpenMode.ForRead) as Line;

                        // Step 3: Get endpoint selection from user
                        Point3d labelPosition;
                        if (polyline == null && line == null)
                        {
                            ed.WriteMessage("\nSelected entity is not a polyline or a line.");
                            continue;
                        }

                        if (polyline != null)
                        {
                            // Allow user to select an endpoint of the polyline
                            labelPosition = GetEndpointFromUser(ed, polyline);
                            if (labelPosition == Point3d.Origin) continue; // User cancelled
                        }
                        else // it's a line
                        {
                            // Allow user to select an endpoint of the line
                            labelPosition = GetEndpointFromUser(ed, line);
                            if (labelPosition == Point3d.Origin) continue; // User cancelled
                        }

                        // Get the offset direction based on the selected endpoint
                        Vector3d offsetDirection = labelPosition == (polyline != null ? polyline.GetPoint3dAt(0) : line.StartPoint)
                            ? new Vector3d(-1, 0, 0)
                            : new Vector3d(1, 0, 0);

                        // Offset the label position
                        double offsetDistance = 1.5; // Set the desired distance from the line
                        labelPosition = labelPosition.Add(offsetDirection.GetNormal() * offsetDistance);

                        AttachmentPoint attachment = LineDirection(offsetDirection);

                        // Step 2: Get label text from user
                        string rackNumber = GetUserInput(ed, "\nEnter Rack #:");
                        if (string.IsNullOrEmpty(rackNumber)) return;

                        string device = GetUserInput(ed, "\nEnter Device:");
                        if (string.IsNullOrEmpty(device)) return;

                        string inputOutput = GetUserInput(ed, "\nEnter Input/Output:");
                        if (string.IsNullOrEmpty(inputOutput)) return;

                        string drawingLabel = GetUserInput(ed, "\nEnter Drawing Label:");
                        if (string.IsNullOrEmpty(drawingLabel)) return;

                        // Format the label text
                        string labelText = $"{rackNumber}, {device}, {inputOutput}, [{drawingLabel}]";

                        // Create and add the label
                        MText mText = new MText
                        {
                            Location = labelPosition,
                            Contents = labelText,
                            Height = 1, // Adjust height as needed
                            TextHeight = 1.8,
                            Attachment = attachment
                        };

                        // Step 5: Add the MText to the model space
                        btr.AppendEntity(mText);
                        tr.AddNewlyCreatedDBObject(mText, true);

                        tr.Commit();
                    }
                }
            }
        }

        private Point3d GetEndpointFromUser(Editor ed, Polyline polyline)
        {
            PromptPointOptions ppo = new PromptPointOptions("\nSelect endpoint of polyline: ");
            PromptPointResult ppr = ed.GetPoint(ppo);
            if (ppr.Status != PromptStatus.OK) return Point3d.Origin;

            return ppr.Value;
        }

        private Point3d GetEndpointFromUser(Editor ed, Line line)
        {
            PromptPointOptions ppo = new PromptPointOptions("\nSelect endpoint of line: ");
            PromptPointResult ppr = ed.GetPoint(ppo);
            if (ppr.Status != PromptStatus.OK) return Point3d.Origin;

            return ppr.Value;
        }

        private AttachmentPoint LineDirection(Vector3d normalDirection)
        {
            AttachmentPoint attachment;
            if (normalDirection.X < 0)
            {
                attachment = AttachmentPoint.MiddleRight;
            }
            else if (normalDirection.X > 0)
            {
                attachment = AttachmentPoint.MiddleLeft;
            }
            else
            {
                if (normalDirection.Y < 0)
                {
                    attachment = AttachmentPoint.TopCenter;
                }
                else if (normalDirection.Y > 0)
                {
                    attachment = AttachmentPoint.BottomCenter;
                }
                else
                {
                    attachment = AttachmentPoint.MiddleCenter; // Edge case
                }
            }

            return attachment;
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

        private string GetUserInput(Editor ed, string message)
        {
            PromptStringOptions pso = new PromptStringOptions(message);
            pso.AllowSpaces = true; // Allow spaces in input
            PromptResult pr = ed.GetString(pso);
            return pr.Status == PromptStatus.OK ? pr.StringResult : null;
        }
    }
}