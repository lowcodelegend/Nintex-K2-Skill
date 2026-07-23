using System;
using System.Linq;
using System.Xml.Linq;

namespace K2SmartFormsCli
{
    internal static class ViewChartLayoutDefinition
    {
        public static string Apply(string xml, ViewDefinition view)
        {
            if (view.Charts == null || view.Charts.Count == 0) return xml;
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            var root = document.Descendants().First(x => x.Name.LocalName == "View" && x.Attribute("ID") != null);
            var ns = root.Name.Namespace;
            var controls = root.Elements().First(x => x.Name.LocalName == "Controls");
            var events = root.Elements().First(x => x.Name.LocalName == "Events");
            var init = events.Elements().FirstOrDefault(x => x.Name.LocalName == "Event" && string.Equals(Child(x, "Name"), "Init", StringComparison.OrdinalIgnoreCase));
            var execute = init == null ? null : init.Descendants().FirstOrDefault(x => x.Name.LocalName == "Action" && string.Equals((string)x.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase) && string.Equals(ActionPropertyOrNull(x, "Method"), view.DefaultListMethod, StringComparison.OrdinalIgnoreCase));
            if (execute == null) execute = BuildListActionTemplate(ns, root, view.DefaultListMethod);
            var objectProperty = ActionPropertyElementOrNull(execute, "ObjectID");
            var sourceResult = execute.Descendants().FirstOrDefault(x => x.Name.LocalName == "Result" && x.Attribute("SourceID") != null);
            if (objectProperty == null && sourceResult == null) throw new CliException("Generated View '" + view.Name + "' List action exposes no SmartObject result for chart binding.");
            var objectId = objectProperty == null ? (string)sourceResult.Attribute("SourceID") : Child(objectProperty, "Value");
            if (objectProperty == null)
                objectProperty = new XElement(ns + "Property", new XElement(ns + "Name", "ObjectID"),
                    new XElement(ns + "DisplayValue", (string)sourceResult.Attribute("SourceDisplayName") ?? view.SmartObject),
                    new XElement(ns + "NameValue", (string)sourceResult.Attribute("SourceName") ?? view.SmartObject), new XElement(ns + "Value", objectId));
            var viewId = (string)root.Attribute("ID");
            var bodyGrid = root.Descendants().First(x => x.Name.LocalName == "Section" && string.Equals((string)x.Attribute("Type"), "Body", StringComparison.OrdinalIgnoreCase)).Elements().First(x => x.Name.LocalName == "Control");
            var columns = bodyGrid.Elements().FirstOrDefault(x => x.Name.LocalName == "Columns");
            var columnCount = columns == null ? 1 : columns.Elements().Count(x => x.Name.LocalName == "Column");
            var rows = bodyGrid.Elements().First(x => x.Name.LocalName == "Rows");
            rows.Elements().Where(x => x.Name.LocalName == "Row").Remove();
            foreach (var chart in view.Charts.AsEnumerable().Reverse())
            {
                var id = NewId();
                controls.Add(BuildChart(ns, id, chart, objectProperty, view.DefaultListMethod));
                rows.AddFirst(new XElement(ns + "Row", new XAttribute("ID", NewId()), new XElement(ns + "Cells",
                    new XElement(ns + "Cell", new XAttribute("ID", NewId()), new XAttribute("ColumnSpan", Math.Max(1, columnCount)), new XElement(ns + "Control", new XAttribute("ID", id))))));
                events.Add(BuildInitializingEvent(ns, id, chart, viewId, objectId, objectProperty, execute));
            }
            return document.ToString(SaveOptions.DisableFormatting);
        }

        public static void Verify(string xml, ViewDefinition view)
        {
            if (view.Charts == null || view.Charts.Count == 0) return;
            var document = XDocument.Parse(xml);
            foreach (var chart in view.Charts)
            {
                var control = document.Descendants().SingleOrDefault(x => x.Name.LocalName == "Control" && string.Equals((string)x.Attribute("Type"), "GenericChart", StringComparison.OrdinalIgnoreCase) && string.Equals(Child(x, "Name"), chart.Name, StringComparison.OrdinalIgnoreCase));
                if (control == null) throw new CliException("K2 View '" + view.Name + "' is missing chart '" + chart.Name + "'.");
                var id = (string)control.Attribute("ID");
                if (!document.Descendants().Any(x => x.Name.LocalName == "Event" && string.Equals((string)x.Attribute("SourceID"), id, StringComparison.OrdinalIgnoreCase) && string.Equals(Child(x, "Name"), "Initializing", StringComparison.OrdinalIgnoreCase)))
                    throw new CliException("K2 View '" + view.Name + "' chart '" + chart.Name + "' has no initializing data rule.");
            }
        }

        private static XElement BuildChart(XNamespace ns, string id, ViewChartDefinition chart, XElement objectProperty, string method)
        {
            var type = chart.Type == "donut" ? "donut" : chart.Type;
            return new XElement(ns + "Control", new XAttribute("ID", id), new XAttribute("Type", "GenericChart"),
                new XElement(ns + "Name", chart.Name), new XElement(ns + "DisplayName", chart.Name),
                new XElement(ns + "Properties", P(ns,"ControlName",chart.Name),P(ns,"HeaderText",chart.Title),P(ns,"TitleVisible","true"),P(ns,"WatermarkText",chart.EmptyState),
                    P(ns,"DataSourceType","SmartObject"),CopyProperty(ns,"AssociationSO",objectProperty),P(ns,"AssociationMethod",method),
                    P(ns,"CategoryAxisProperty",chart.CategoryProperty),P(ns,"CategoryAxisDisplayName",chart.CategoryProperty),P(ns,"ValueAxisProperty",chart.ValueProperty),P(ns,"ValueAxisDisplayName",chart.ValueProperty),
                    P(ns,"ChartType",type),P(ns,"DataType","Number"),P(ns,"SeriesAggregate","max"),P(ns,"SeriesMarkerSize","6"),P(ns,"SeriesMarkersVisible","true"),P(ns,"SeriesSpacing","0.4"),P(ns,"SeriesGap","1.5"),
                    P(ns,"ValueDisplayUnit","auto"),P(ns,"ValueInputUnit","number"),P(ns,"ValueFriendlyDurations","false"),P(ns,"ValueLabelPosition","insideEnd"),P(ns,"ValueLabelsVisible",chart.ShowLabels.ToString().ToLowerInvariant()),
                    P(ns,"ValueAxisLabelFormat","{0}"),P(ns,"ValueAxisType","numeric"),P(ns,"ValueAxisReverse","false"),P(ns,"ValueAxisMinorGridLinesVisible","false"),P(ns,"ValueAxisMajorGridLinesVisible","true"),P(ns,"ValueAxisLabelRotation","autorotate"),P(ns,"ValueAxisLabelStep","1"),P(ns,"ValueAxisLabelVisible","true"),P(ns,"ValueAxisTitlePosition","center"),P(ns,"ValueAxisTitleRotation","twoseventy"),P(ns,"ValueAxisTitleVisible","false"),
                    P(ns,"CategoryAxisWeekStartDay","0"),P(ns,"CategoryAxisMaxDateGroups","10"),P(ns,"CategoryAxisLabelDateFormatYears","yyyy"),P(ns,"CategoryAxisLabelDateFormatWeeks","M/d"),P(ns,"CategoryAxisLabelDateFormatMonths","MMM yy"),P(ns,"CategoryAxisLabelDateFormatHours","HH:mm"),P(ns,"CategoryAxisLabelDateFormatDays","M/d"),P(ns,"CategoryAxisLabelFormat","{0}"),P(ns,"CategoryAxisType","category"),P(ns,"CategoryAxisReverse","false"),P(ns,"CategoryAxisMinorGridLinesVisible","false"),P(ns,"CategoryAxisMajorGridLinesVisible","false"),P(ns,"CategoryAxisLabelRotation","autorotate"),P(ns,"CategoryAxisLabelStep","1"),P(ns,"CategoryAxisJustified","false"),P(ns,"CategoryAxisLabelVisible","true"),P(ns,"CategoryAxisTitlePosition","center"),P(ns,"CategoryAxisTitleRotation","zero"),P(ns,"CategoryAxisTitleVisible","false"),
                    P(ns,"LegendReverse","false"),P(ns,"LegendOrientation","horizontal"),P(ns,"LegendHorizontalAlignment","center"),P(ns,"LegendPosition","bottom"),P(ns,"LegendVisible",chart.ShowLegend.ToString().ToLowerInvariant()),P(ns,"ChartZoomable","false"),P(ns,"ChartPannable","false"),P(ns,"TooltipVisible","true"),P(ns,"ChartStackType","none"),P(ns,"TitleVerticalAlignment","top"),P(ns,"TitleHorizontalAlignment","center"),P(ns,"HeaderVisible","true"),P(ns,"Height",chart.Height+"px"),P(ns,"Transitions","true")));
        }

        private static XElement BuildInitializingEvent(XNamespace ns,string id,ViewChartDefinition chart,string viewId,string objectId,XElement objectProperty,XElement sourceExecute)
        {
            var action=new XElement(sourceExecute); action.SetAttributeValue("ID",NewId()); action.SetAttributeValue("DefinitionID",NewId());
            var props=action.Elements().First(x=>x.Name.LocalName=="Properties"); SetActionProperty(props,"Location","Control"); SetActionProperty(props,"ControlID",id,chart.Name,chart.Name);
            var results=action.Elements().FirstOrDefault(x=>x.Name.LocalName=="Results"); if(results!=null)results.Remove();
            action.Add(new XElement(ns+"Results",new XElement(ns+"Result",new XAttribute("SourceID",objectId),new XAttribute("SourceName",Child(objectProperty,"NameValue")??string.Empty),new XAttribute("SourceDisplayName",Child(objectProperty,"DisplayValue")??string.Empty),new XAttribute("SourceType","Result"),new XAttribute("TargetID",id),new XAttribute("TargetName",chart.Name),new XAttribute("TargetDisplayName",chart.Name),new XAttribute("TargetType","Control"))));
            return new XElement(ns+"Event",new XAttribute("ID",NewId()),new XAttribute("DefinitionID",NewId()),new XAttribute("Type","User"),new XAttribute("SourceID",id),new XAttribute("SourceType","Control"),new XAttribute("SourceName",chart.Name),new XAttribute("SourceDisplayName",chart.Name),new XElement(ns+"Name","Initializing"),new XElement(ns+"DisplayName","Initializing"),new XElement(ns+"Properties",P(ns,"ViewID",viewId),P(ns,"RuleFriendlyName","When "+chart.Name+" is Initializing"),P(ns,"Location",viewId)),new XElement(ns+"Handlers",new XElement(ns+"Handler",new XAttribute("ID",NewId()),new XAttribute("DefinitionID",NewId()),new XElement(ns+"Properties",P(ns,"HandlerName","IfLogicalHandler"),P(ns,"Location","control")),new XElement(ns+"Actions",action))));
        }
        private static XElement BuildListActionTemplate(XNamespace ns,XElement root,string method)
        {
            var source=root.Elements().First(x=>x.Name.LocalName=="Sources").Elements().First(x=>x.Name.LocalName=="Source"&&string.Equals((string)x.Attribute("ContextType"),"Primary",StringComparison.OrdinalIgnoreCase));
            var objectId=(string)source.Attribute("SourceID");var sourceName=(string)source.Attribute("SourceName")??string.Empty;var display=(string)source.Attribute("SourceDisplayName")??sourceName;var viewId=(string)root.Attribute("ID");var viewName=Child(root,"Name")??string.Empty;
            var objectProperty=P(ns,"ObjectID",objectId);Set(objectProperty,"DisplayValue",display);Set(objectProperty,"NameValue",sourceName);var viewProperty=P(ns,"ViewID",viewId);Set(viewProperty,"DisplayValue",viewName);Set(viewProperty,"NameValue",viewName);
            return new XElement(ns+"Action",new XAttribute("ID",NewId()),new XAttribute("DefinitionID",NewId()),new XAttribute("Type","Execute"),new XAttribute("ExecutionType","Synchronous"),
                new XElement(ns+"Properties",P(ns,"Location","Control"),P(ns,"Method",method),viewProperty,objectProperty),new XElement(ns+"Results",
                    new XElement(ns+"Result",new XAttribute("SourceID",objectId),new XAttribute("SourceName",sourceName),new XAttribute("SourceDisplayName",display),new XAttribute("SourceType","Result"))));
        }
        private static XElement CopyProperty(XNamespace ns,string name,XElement source){var p=P(ns,name,Child(source,"Value"));Set(p,"DisplayValue",Child(source,"DisplayValue"));Set(p,"NameValue",Child(source,"NameValue"));return p;}
        private static XElement P(XNamespace ns,string name,string value){return new XElement(ns+"Property",new XElement(ns+"Name",name),new XElement(ns+"DisplayValue",value??string.Empty),new XElement(ns+"NameValue",value??string.Empty),new XElement(ns+"Value",value??string.Empty));}
        private static void SetActionProperty(XElement props,string name,string value,string display=null,string nameValue=null){var p=props.Elements().FirstOrDefault(x=>x.Name.LocalName=="Property"&&string.Equals(Child(x,"Name"),name,StringComparison.OrdinalIgnoreCase));if(p==null){p=P(props.Name.Namespace,name,value);props.Add(p);}Set(p,"Value",value);Set(p,"DisplayValue",display??value);Set(p,"NameValue",nameValue??value);}
        private static XElement ActionPropertyElement(XElement action,string name){return action.Elements().Where(x=>x.Name.LocalName=="Properties").SelectMany(x=>x.Elements()).First(x=>x.Name.LocalName=="Property"&&string.Equals(Child(x,"Name"),name,StringComparison.OrdinalIgnoreCase));}
        private static XElement ActionPropertyElementOrNull(XElement action,string name){return action.Elements().Where(x=>x.Name.LocalName=="Properties").SelectMany(x=>x.Elements()).FirstOrDefault(x=>x.Name.LocalName=="Property"&&string.Equals(Child(x,"Name"),name,StringComparison.OrdinalIgnoreCase));}
        private static string ActionProperty(XElement action,string name){return Child(ActionPropertyElement(action,name),"Value");}
        private static string ActionPropertyOrNull(XElement action,string name){var p=action.Elements().Where(x=>x.Name.LocalName=="Properties").SelectMany(x=>x.Elements()).FirstOrDefault(x=>x.Name.LocalName=="Property"&&string.Equals(Child(x,"Name"),name,StringComparison.OrdinalIgnoreCase));return p==null?null:Child(p,"Value");}
        private static string Child(XElement e,string name){var c=e.Elements().FirstOrDefault(x=>x.Name.LocalName==name);return c==null?null:c.Value;}
        private static void Set(XElement e,string name,string value){var c=e.Elements().FirstOrDefault(x=>x.Name.LocalName==name);if(c==null){c=new XElement(e.Name.Namespace+name);e.Add(c);}c.Value=value??string.Empty;}
        private static string NewId(){return Guid.NewGuid().ToString();}
    }
}
