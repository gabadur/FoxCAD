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
