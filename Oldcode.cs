/*[CommandMethod("DrawSignalArrow")]
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
    DrawArrow(trans, midpoint, direction);
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
            rightpoint = polyline.GetPoint3dAt(i+1);
            break;
        }
        accumulatedLength += segmentLength;
    }

    // Calculate the direction from input to output
    Point3d halfwayPoint=polyline.GetPoint3dAt(0);
    if (polyline.GetPoint3dAt(0)== inputPoint)
    {
        direction = rightpoint- leftpoint;
        halfwayPoint = leftpoint + direction * 0.5;
    }
    else
    {
        direction = leftpoint - rightpoint;
        halfwayPoint = rightpoint + direction * 0.5;

    } 



    DrawArrow(trans, halfwayPoint, direction);
}

private void DrawArrow(Transaction trans, Point3d position, Vector3d direction)
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
}*/



/*[CommandMethod("DrawSignalArrow")]
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

        // Determine if entity is Line or Polyline aa
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
    Point3d inputPoint = GetEndpoint("Select the input endpoint:", line, trans);
    if (inputPoint == Point3d.Origin) return;

    Point3d outputPoint = GetEndpoint("Select the output endpoint:", line, trans);
    if (outputPoint == Point3d.Origin) return;

    Point3d midpoint = new Point3d((line.StartPoint.X + line.EndPoint.X) / 2,
                                     (line.StartPoint.Y + line.EndPoint.Y) / 2,
                                     (line.StartPoint.Z + line.EndPoint.Z) / 2);

    Vector3d direction = outputPoint - inputPoint;

    // Create the arrow
    Polyline arrow = CreateArrow(midpoint, direction, trans);

    // Create a new polyline to combine
    using (Polyline combined = new Polyline())
    {
        // Add the segments from the line
        combined.AddVertexAt(0, new Point2d(line.StartPoint.X, line.StartPoint.Y), 0, 0, 0);
        combined.AddVertexAt(1, new Point2d(line.EndPoint.X, line.EndPoint.Y), 0, 0, 0);

        // Add the arrow segments
        for (int i = 0; i < arrow.NumberOfVertices; i++)
        {
            combined.AddVertexAt(combined.NumberOfVertices,
                new Point2d(arrow.GetPoint3dAt(i).X, arrow.GetPoint3dAt(i).Y), 0, 0, 0);
        }

        combined.Layer = line.Layer;

        BlockTableRecord btr = (BlockTableRecord)trans.GetObject(line.Database.CurrentSpaceId, OpenMode.ForWrite);
        btr.AppendEntity(combined);
        trans.AddNewlyCreatedDBObject(combined, true);

        // Optionally delete the original line if you want to keep only the combined
        trans.GetObject(line.ObjectId, OpenMode.ForWrite).Erase();
    }
}

private void DrawArrowOnPolyline(Transaction trans, Polyline polyline)
{
    Point3d inputPoint = GetEndpoint("Select the input endpoint:", polyline, trans);
    if (inputPoint == Point3d.Origin) return;

    Point3d outputPoint = GetEndpoint("Select the output endpoint:", polyline, trans);
    if (outputPoint == Point3d.Origin) return;

    // Calculate the midpoint as done previously
    double totalLength = 0.0;
    for (int i = 0; i < polyline.NumberOfVertices - 1; i++)
    {
        totalLength += polyline.GetPoint3dAt(i).DistanceTo(polyline.GetPoint3dAt(i + 1));
    }

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

    Vector3d arrowDirection = outputPoint - inputPoint;
    Polyline arrow = CreateArrow(midpoint, arrowDirection, trans);

    // Create a new polyline to combine
    using (Polyline combined = new Polyline())
    {
        // Add the segments from the original polyline
        for (int i = 0; i < polyline.NumberOfVertices; i++)
        {
            combined.AddVertexAt(i, new Point2d(polyline.GetPoint3dAt(i).X, polyline.GetPoint3dAt(i).Y), 0, 0, 0);
        }

        // Add the arrow segments
        for (int i = 0; i < arrow.NumberOfVertices; i++)
        {
            combined.AddVertexAt(combined.NumberOfVertices,
                new Point2d(arrow.GetPoint3dAt(i).X, arrow.GetPoint3dAt(i).Y), 0, 0, 0);
        }

        combined.Layer = polyline.Layer;

        BlockTableRecord btr = (BlockTableRecord)trans.GetObject(polyline.Database.CurrentSpaceId, OpenMode.ForWrite);
        btr.AppendEntity(combined);
        trans.AddNewlyCreatedDBObject(combined, true);

        // Optionally delete the original polyline if you want to keep only the combined
        trans.GetObject(polyline.ObjectId, OpenMode.ForWrite).Erase();
    }
}

private Polyline CreateArrow(Point3d position, Vector3d direction, Transaction trans)
{
    Document doc = Application.DocumentManager.MdiActiveDocument;
    Database db = doc.Database;

    // Normalize direction
    direction = direction.GetNormal();
    double arrowLength = 1; // Length for the arrow
    double triangleangle = Math.PI / 6;

    // Calculate the arrow tip based on the position
    Point3d arrowTip = position;
    Point3d arrowBase1 = arrowTip - direction.RotateBy(triangleangle, Vector3d.ZAxis) * arrowLength;
    Point3d arrowBase2 = arrowTip - direction.RotateBy(-triangleangle, Vector3d.ZAxis) * arrowLength;

    // Create arrow shape
    Polyline arrow = new Polyline(3);
    arrow.AddVertexAt(0, new Point2d(arrowTip.X, arrowTip.Y), 0, 0, 0);
    arrow.AddVertexAt(1, new Point2d(arrowBase1.X, arrowBase1.Y), 0, 0, 0);
    arrow.AddVertexAt(2, new Point2d(arrowBase2.X, arrowBase2.Y), 0, 0, 0);
    arrow.Closed = true; // Close the arrow shape

    return arrow;
}

private Point3d GetEndpoint(string message, Entity entity, Transaction trans)
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
}*/


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





/* [CommandMethod("CreateArrow")]
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

        if (paramNextPoint > polyline.Length)
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
} */

//correct code for single line




/*
[CommandMethod("CreateArrow", CommandFlags.UsePickSet)]
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

        if (paramNextPoint > polyline.Length)
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
        double arrowWidth = 1.0;   // This controls the width of the arrowhead

        // Create the arrow polyline (this will be part of the original polyline)
        Polyline arrow = new Polyline();
        arrow.AddVertexAt(0, new Point2d(selectedPoint.X, selectedPoint.Y), 0, 0, arrowWidth); // Start point
        arrow.AddVertexAt(1, new Point2d(selectedPoint.X + direction.X * arrowLength, selectedPoint.Y + direction.Y * arrowLength), 0, 0, arrowWidth); // End point

        // Add the arrow polyline to the current space but don't append it independently
        BlockTableRecord btr = tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;

        // Instead of appending the arrow as a separate entity, join it directly to the polyline
        polyline.UpgradeOpen();
        try
        {
            // Now, add the arrow to the polyline directly
            polyline.JoinEntity(arrow);
        }
        catch (Autodesk.AutoCAD.Runtime.Exception ex)
        {
            ed.WriteMessage($"\nERROR: Can only connect arrow to endpoints of line. Endpoint was not selected. Arrow was not joined with line.");
            return;
        }

        // Commit the transaction
        tr.Commit();
    }

    ed.WriteMessage($"\nArrow created and polyline joined successfully.");
}
*/

/*
[CommandMethod("LineArrow", CommandFlags.UsePickSet)]
public void LineArrow()
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
            Application.ShowAlertDialog("Object Selected is not a Line");
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
*/





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