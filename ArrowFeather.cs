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
            for (int i = selectedEntities.Count - 1; i >= 0; i--)
                {
                    SelectedObject selectedObj = selectedEntities[i];
                    using (Transaction tr = doc.TransactionManager.StartTransaction())
                    {
                        BlockTableRecord btr = tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;
                        Polyline polyline = tr.GetObject(selectedObj.ObjectId, OpenMode.ForRead) as Polyline;
                        Line line = tr.GetObject(selectedObj.ObjectId, OpenMode.ForRead) as Line;
                        // Step 2: Get label text from user
                        PromptStringOptions pso = new PromptStringOptions("\nEnter label text:");
                        PromptResult pr = ed.GetString(pso);
                        if (pr.Status != PromptStatus.OK) return;

                        string labelText = pr.StringResult;

                        // Step 3: Calculate position for label
                        Point3d labelPosition;
                        Vector3d offsetDirection = new Vector3d(0, 0, 0);
                        AttachmentPoint attachment;
                        if (polyline == null && line == null)
                        {
                            ed.WriteMessage("\nSelected entity is not a polyline or a line.");
                            continue;
                        }

                        else if (line == null) //its a polyline
                        {
                            // Use the end point of the polyline
                            labelPosition = polyline.GetPoint3dAt(polyline.NumberOfVertices - 1);
                            // Get the direction to the second-to-last vertex for offset
                            if (polyline.NumberOfVertices > 1)
                            {
                                var direction = polyline.GetPoint3dAt(polyline.NumberOfVertices - 1) - (polyline.GetPoint3dAt(polyline.NumberOfVertices - 2));
                            //offsetDirection = direction.GetPerpendicularVector().GetNormal();
                            offsetDirection = direction.GetNormal();
                        }
                    }
                        else // its a line
                        {
                            // Use the end point of the line
                            labelPosition = line.EndPoint;
                            //offsetDirection = line.Delta.GetPerpendicularVector().GetNormal();
                            offsetDirection = line.Delta.GetNormal();
                        }

                        // Offset the label position
                        double offsetDistance = 0.1; // Set the desired distance from the line
                        labelPosition = labelPosition.Add(offsetDirection * offsetDistance);
                    
                        attachment = LineDirection(offsetDirection);


                    // Step 4: Create and add the label
                    MText mText = new MText
                        {
                            Location = labelPosition,
                            Contents = labelText,
                            Height = 1, // Adjust height as needed
                            TextHeight = 0.8,
                            Attachment = attachment
                    };

                        // Step 5: Add the MText to the model space
                        btr.AppendEntity(mText);
                        tr.AddNewlyCreatedDBObject(mText, true);

                        tr.Commit();
                    }
            }
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
    }

}