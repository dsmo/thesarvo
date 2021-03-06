using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.IO;
using InDesign;


namespace GuideLayout
{
    class Guide
    {
        public Document document;
        public Application application;
        public BookContent bookContent;

        IList<GuidePage> pages = new List<GuidePage>();

        public GuidePage currentPage = null;

        public XmlDocument xml = null;

        IList<string> classes = new List<string>();

        //Type miss = Type.Missing;
        object miss = System.Reflection.Missing.Value;

        

        public const int INDENTED = 18;


        const double GRAPHSIZE = 32;
        const double ICONSIZE = 10;

        public int sideBarMax = 0;
        public int sideBarCount = 0;

        const double SPACEAFTERCLIMB = 1.5;

        public Guide(Application app, Document doc)
        {
            this.document = doc;
            this.application = app;
            
        }

        public void LayoutGuide(bool forceUpdate)
        {
            bool updateNeeded = this.UpdateLinks();

            bool manageSpread = GetConfigBool(document.Name, "ManageSpread");
            bool swingingThird = GetConfigBool(document.Name,"SwingingThird");

            //if (document.Name == "The Star Factory.indd")
            //    forceUpdate = true;

            if (forceUpdate || updateNeeded)
            {
                Log("Updating " + document.Name + "...");

                DoUpdate();

                SaveAndClose();
            }
            else if (manageSpread)
            {
                ManageSpread(swingingThird);
            }
            else
            {
                Log("Not updating " + document.Name);
                Close();
            }

            

        }

        public void DoUpdate()
        {
            

            
            bool loaded = LoadXml();

            if (!loaded || xml==null)
                Log("!! Could not load xml");
            else
            {
                ClearPages();

                currentPage = GetPage(0);

                SetRunningColour();
                currentPage.ApplyMaster("FP-Master");

                SetupSidebar();

                DoProcessXml();
                ClearEmptyPages();
            }

            
        }

        public MasterSpread GetMaster(string name)
        {
            try
            {
                return (MasterSpread)document.MasterSpreads[name];
            }
            catch (Exception e)
            {
                Log("Could not get master name=" + name);
                return null;
            }
        }

        void SetRunningColour()
        {
            double[] val = new double[3] { 230, 230, 230 };

            string markerText = ((Section)document.Sections.FirstItem()).Marker;

            if (markerText != null && markerText.Length > 0)
            {
                string col = GetConfig(markerText.Trim() + "." + "RunningColour");
                if (col != null && col.Trim().Length>0)
                {
                    
                    string[] split = col.Split(',');
                    if (split.Length == 3)
                    {
                        val[0] = Double.Parse(split[0]);
                        val[1] = Double.Parse(split[1]);
                        val[2] = Double.Parse(split[2]);
                    }
                   
                }
            }

            Color color = GetColor("RunningColour", idColorModel.idProcess, val);
        }

        void SetupSidebar()
        {
            if (sideBarMax > 0)
            {
                double maxWidth = 40;
                double height = GetPage(0).contentBounds.height;
                double topMargin = GetPage(0).contentBounds.top;
                double top;

                if (sideBarCount > sideBarMax)
                    Log("!! sideBarCount is bigger than sideBarMax");

                double spacing = (height-maxWidth) / (sideBarMax-1);
                if (spacing > maxWidth)
                    spacing = maxWidth;

                top = topMargin + spacing * (sideBarCount-1);


                MasterSpread master = GetMaster("A-Master");

                TextFrames tfs = master.TextFrames;
                foreach (TextFrame tf in tfs)
                {
                    Bounds b = new Bounds(tf.GeometricBounds);

                    if (tf.Paragraphs.Count > 0)
                    {
                        Paragraph p = (Paragraph) tf.Paragraphs[1];

                        if ( ((ParagraphStyle) p.AppliedParagraphStyle).Name=="SideBar" )
                        {
                            //double h = b.height;
                            b.top = top;
                            b.height = maxWidth;
                            tf.GeometricBounds = b.raw;
                        }
                    }


                    
                }
            }
        }

        public Color GetColor(string name, idColorModel colorModel, double[] value)
        {
            Color ret = null;


            try
            {
                ret = (Color) document.Colors[name];
                //name = myColor.name;
            }
            catch (Exception e)
            {
                ret = document.Colors.Add();
                ret.Name = name;
            }

            if (ret.Space == idColorSpace.idRGB)
            {
                ret.ColorValue = value;
                ret.Model = colorModel;
            }
            else
                Log("Could not set color name=" + name + " because its not RGB!");

            return ret;
        }

        public void DoProcessXml()
        {
            XmlNode guideNode = xml.SelectSingleNode("guide");

            XmlNodeList nodeList = guideNode.ChildNodes;

            foreach (XmlNode node in nodeList)
            {
                string name = node.Name;

                if (node is XmlElement)
                {
                    switch (name)
                    {
                        case "problem":
                        case "climb":
                            ProcessClimb( (XmlElement) node);
                            break;
                        case "text":
                            ProcessText((XmlElement)node);
                            break;
                        case "image":
                            ProcessImage((XmlElement)node);
                            break;
                        case "header":
                            ProcessHeader((XmlElement)node);
                            break;
                        case "gps":
                            ProcessGps((XmlElement) node);
                            break;
                        default:
                            throw new Exception("Unknown node: " + name);

                    }
                }
            }
        }

        void ProcessClimb(XmlElement node)
        {
            string txt = node.InnerText;
            string number = GetAttr(node, "number");
            string name = GetAttr(node, "name");
            string stars = GetAttr(node, "stars");
            string grade = GetAttr(node, "grade");
            string extra = GetAttr(node, "extra");
            string length = GetAttr(node, "length");
            string fa = GetAttr(node, "fa");

            string origStars = stars;
            stars = stars.Replace("*","«");

				
			string heading = "";
			if (stars.Length>0)
				heading += stars + " ";
			if (number.Length>0)
				heading += number+ "  ";
			if (name.Length>0)
				heading += name+ "  ";
			
			if (length.Length>0)
				heading += length + "  ";
			if (grade.Length>0)
				heading += grade + "  ";

            extra = extra.Replace('↓', 'ŏ');


            if (name == "Cascade Crack")
            {
                int x = 0;
            }

            Mode mode = Mode.TwoColumns;
            bool forceNew = false;

            if (currentPage.currentMode == Mode.SingleColumn)
            {
                XmlNode next = node.NextSibling;
                if (next == null || next.Name != node.Name)
                {
                    mode = Mode.SingleColumn;
                    forceNew = true;
                }
            }


			GuideFrame frame = GetTextFrame( mode, FrameType.Multi, forceNew);
			


			//string[] split = txt.Split("\n");
				
            //txt = split[ split.length-1 ];
				
			txt = txt.Trim();
            txt = txt.Replace('\t', ' ');
            txt = txt.Replace("<br/>", "\n");
		    int len = txt.Length;
				
			if (len==1 && txt.ToCharArray()[0]==65279)
			{
				txt = "";
			}

            if (extra.Length > 0)
            {
                extra += " ";
                extra = extra.Replace("B ", "Þ");
                extra = "  " + extra;
            }
				
			int offset = 0;
			if (txt.Length > 0 && name.Length==0  && txt.IndexOf ('\r', 0)<0 )
			{
			   
				extra += "  -  ";
				extra += txt;
				txt = "";
				offset = 2;
			}

            // reformat dates
            txt = FixDates(txt);
            fa = FixDates(fa);
			
            // create and add index entries
            string grade2digits = grade;
            if (grade2digits.Length == 1)
                grade2digits = "0" + grade2digits;

            if (grade2digits.Length > 2)
                grade2digits = grade2digits.Substring(0, 2);

            if (grade2digits.Length == 0)
                grade2digits = "-";

            string nameShort = name;
            if (name.Length > 24)
                nameShort = name.Substring(0, 21) + "...";

            string stars2 = origStars;
            if (stars2.Length == 0)
                stars2 += "   ";
            if (stars2.Length == 1)
                stars2 += "  ";
            if (stars2.Length == 2)
                stars2 += " ";

            string gradeIndex = grade2digits + " " + origStars + "\t" + nameShort;
            string nameIndex = nameShort + "\t" + grade2digits + " " + origStars;
            
            if (grade.Length > 0)
                frame.AddPara(gradeIndex, "gradeIndexTag", false);
            
            frame.AddPara(nameIndex, "alphaIndexTag", false);

            Story story = frame.story;

            int starsStart = story.Characters.Count;

            if (starsStart > 0)
                starsStart++;

			// add header
            InsertionPoint headPara = frame.AddPara(heading, "ClimbHeading", false);
				
			// add extra and unbold

            InsertionPoint ip = (InsertionPoint) story.InsertionPoints.LastItem();

			ip.Contents = extra;


            Characters chars = story.Characters;

			for (int i = chars.Count  ; i > chars.Count - extra.Length ; i--)
			{
				//chars[i].fontStyle = "Regular";
                Character c = (Character) chars[i];
                c.FontStyle = "Regular";
			}
		
			if (stars.Length > 0)
			{
				//var starsStart = chars.length - heading.length - extra.length + offset;
				
				for (int i = starsStart+1  ; i <= starsStart + stars.Length ; i++)
				{
                    Character c = (Character)chars[i];
					c.AppliedFont = this.application.Fonts["Wingdings"];
					c.FontStyle = "Regular";
				}
			}

            headPara.KeepAllLinesTogether = true;
            headPara.KeepLinesTogether = true;
            headPara.KeepFirstLines = 2;
            if (txt.Length > 0 || fa.Length > 0)
            {
                headPara.KeepWithNext = 1;

                if (txt.Length > 0)
                {
                    InsertionPoint ctp = frame.AddPara(txt, "ClimbText", true);

                    if (fa.Length == 0)
                        ctp.SpaceAfter = SPACEAFTERCLIMB;

                    // deal with multi pitch stuff
                    string[] split = txt.Split('\n');
                    for (int i=0; i<split.Length; i++)
                    {
                        string line = split[i].Trim();
                        int idx = line.IndexOf(". ");
                        if (idx>-1 && idx < 16)
                        {
                            int idx2 = line.IndexOf(". ", idx + 1);
                            if (idx2 > -1 && idx2 < 16)
                                idx = idx2;
                            
                            // apply bold - TODO more fancy style
                            Paragraph p = frame.currentParagraphs[i];
                            for (int c = 1; c <= idx; c++)
                            {
                                Character character = p.Characters[c];
                                character.FontStyle = "Bold";
                                character.PointSize = character.PointSize--;
                            }
                        }

                        
                    }

                }

                if (fa.Length > 0)
                {
                    InsertionPoint fap = frame.AddPara(fa, "FA", true);
                    fap.SpaceAfter = SPACEAFTERCLIMB;
                }

            }
            else
            {
                headPara.KeepWithNext = 0;
                headPara.SpaceAfter = SPACEAFTERCLIMB;
            }

            //ResizeToFit(frame);
            frame.ResizeAndPaginate();

			classes.Add("problem");

        }

        string FixDates(string txt)
        {

            //string test = "Doug McConnell February 2003. r, Mar 95. n, 29-4-2003 9/12/2003 9/12/03 Jul/84 Nov/94 Nov/04";

            Match m;
            do
            {
                m = Regex.Match(txt, @"([\d]+)(/|-)([\d]+)(/|-)([\d]+)");

                if (!m.Success)
                    m = Regex.Match(txt, @"(January|February|March|April|June|July|August|September|October|November|December)(/|-|\s)([\d]+)");

                if (!m.Success)
                    m = Regex.Match(txt, @"(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[\.]*(/|-|\s)(6|7|8|9|0)\d");

                if (m.Success)
                {
                    try
                    {
                        DateTime dt = System.DateTime.Parse(m.ToString());

                        string repl = dt.ToString("MMM yyyy");
                        txt = txt.Substring(0, m.Index) + repl + txt.Substring(m.Index + m.Length);

                        Console.WriteLine(">>Replaced '" + m.ToString() + "' with '" + repl + "'");
                    }
                    catch (FormatException e)
                    {
                        Log("Error: could not parse '" + m.ToString() + "' as date");
                        throw e;
                    }
                }

            } while (m.Success);

            //Console.WriteLine(test);
            //Console.ReadLine();
            //return;

            return txt;
        }

        string GetAttr(XmlElement node, string name)
        {
            XmlAttribute attr = node.Attributes[name];
            String ret = "";

            if (attr != null)
                ret = attr.InnerText;

            
            if (ret==null)
                ret = "";

            ret = ret.Replace("&apos;", "\'");

            return ret.Trim();
        }

        void ProcessText(XmlElement node)
        {
            string style = GetAttr(node, "class");

            if (style == "noPrint")
                return;

            Mode requiredMode = Mode.SingleColumn;
            Mode currentMode = currentPage.currentMode;

            bool forceNew = true;
            bool isHeading = Util.IsHeading(style);

            //if (style.indexOf('heading')<0 && style!='text' && style!='Discussion')
			//		style = 'text';

            if (!isHeading && style != "Discussion" && style != "indentedHeader" )
                style = "text";

            string nextNode = Util.GetNextNodeName(node);


            if (currentMode == Mode.TwoColumns && !isHeading)
            {
                if (nextNode == "problem" || nextNode == "climb"
                    || nextNode == "text" )
                {
                    requiredMode = Mode.TwoColumns;
                    forceNew = false;

                    if (style == "text")
                        style = "Discussion";
                }
            }

            

            string text = node.InnerText;

            text = HandleText(text);
            text = text.Replace('↓', 'ŏ');
            text = text.Replace("<br/>", "\n");
            

            text = text.Replace("\n* ", "\n• ");

            bool newPage = GetConfigBool(text, "NewPage");
            if (newPage)
            {
                NextPage();
            }

            int colon = text.IndexOf(":");

            if (style != "indentedHeader" || colon < 0)
            {
                FrameType type = FrameType.Text;

                if (isHeading)
                    type = FrameType.Heading;

                GuideFrame frame = GetTextFrame(requiredMode, type, forceNew);

                if (style == "heading1" && node.PreviousSibling==null)
                {
                    frame.bounds.top -= 4;
                    frame.ApplyBounds();
                    frame.bottomOffset+=4;
                }
                else if (style == "heading2" || (style == "heading3") )
                {
                    frame.bounds.top++;
                    if (style == "heading3")
                        frame.bottomOffset -= 0.5;

                    frame.ApplyBounds();
                }
                
                if (text != null && text.Length > 0)
                {
                    InsertionPoint ip = frame.AddPara(text, style, true);
                    ip.KeepFirstLines = 10;
                }

                frame.ResizeAndPaginate();
            }
            else
            {
                string head = text.Substring(0, colon).Trim();
                string rem = text.Substring(colon + 1).Trim();

                DoIndentedText(rem, head);
            }
            
        }

        private static string HandleText(string text)
        {
            
            text = text.Replace("<br/>", "\r\n");
            text = text.Trim();
            return text;
        }

        private void NextPage()
        {
            currentPage = GetPage(currentPage.idx + 1);
        }



        void ProcessImage(XmlElement node)
        {
            string src = GetAttr(node, "src");
            string width = GetAttr(node, "width");
            string noPrint = GetAttr(node, "noPrint");
    		
	        if (noPrint=="true")
		        return;

            bool forceTwoColums = GetConfigBool(src, "TwoColumns");
            bool fitToPage = GetConfigBool(src, "FitToPage");
            bool rotate = GetConfigBool(src, "Rotate");
            bool border = GetConfigBool(src, "Border");
            string overrideWidth = GetConfig(src, "Width");
            if (overrideWidth != null && overrideWidth.Length > 0)
                width = overrideWidth;

            //int topOffset = 0;
    		
	        if ( src.Trim().Length > 0 )
	        {					

		        if (src=="starwars.jpg")
		        {
			        int x=0;
		        }

                string filename = GetAttachmentPath(src);
                


		        string filenameRepl = filename.Replace(".png",".pdf").Replace(".jpg",".pdf").Replace(".gif",".pdf");
    			
		        bool pdf = false;
                if (filenameRepl != filename && System.IO.File.Exists(filenameRepl))
                {
                    filename = filenameRepl;
                    pdf = true;
                }
                else
                {
                    string srcpdf = src.Replace(".png", ".pdf").Replace(".jpg", ".pdf").Replace(".gif", ".pdf");
                    string filecache = Layout.GetFileCache(srcpdf);
                    if (filecache != null)
                    {
                        filename = filecache;
                        pdf = true;
                    }
                }

                idPDFCrop crop = idPDFCrop.idCropArt;

                Bounds tempBounds = new Bounds( new double[] {0.0, 0.0, 10.0, 10.0} );
                Rectangle temprect = PlaceImageInRect(filename, tempBounds, false, crop);
                
                if (temprect == null)
                {
                    Console.WriteLine("Failed to place image " + src + ", continuing");
                    return;
                }

		        object tempimg =  temprect.AllGraphics.FirstItem();
                Bounds tempbounds = Util.GetImageBounds(tempimg);
                

		        double sizeY = tempbounds.height;

		        double dpi = 72;
                if (!pdf)
                {
                    object[] dpis = ((Image)tempimg).ActualPpi;
                    dpi = (double)dpis[0];
                }
		        double sizeX = tempbounds.width;
		        temprect.Delete();

                if (rotate)
                {
                    double temp = sizeY;
                    sizeY = sizeX;
                    sizeX = temp;
                }
    			
		        Mode type = Mode.SingleColumn;
		        double colsize = 120;
		        bool forceNew = true;
    			
		        sizeX = (dpi/72) * sizeX ;
		        sizeY = (dpi/72) * sizeY;
    			
		        double newSX = sizeX;
		        double newSY = sizeY;

                double w = 0;
                if (width.Length > 0)
                    w = Double.Parse(width);

                

		        if (forceTwoColums || (!pdf && ( sizeX < 180 || (width.Length > 0 && w<=500) )  ))
		        {
			        // can fit in column - this needs more logic though
			        type = Mode.TwoColumns;

                    if (currentPage.currentFrame != null &&
                        currentPage.currentFrame.mode == Mode.TwoColumns)
                        currentPage.currentFrame.bottomOffset += 2;

			        //forceNew = false; 
			        colsize = 58;

                    //if (currentPage.currentFrame != null)
                    //    currentPage.currentFrame.bottomOffset = 2;

                    			        
			        if (width.Length > 0 && !forceTwoColums)
			        {
				        colsize = (w/500) * colsize;
			        }
			        
		        }
		        else
		        {
			        if (width.Length > 0)
			        {
				        colsize = (w/800) * colsize;
			        }
		        }

                GuideFrame frame = GetTextFrame(type, FrameType.Image, forceNew);
                
                frame.AddPara("", "text", false);

                if (fitToPage)
                {
                    newSY = frame.page.contentBounds.bottom - frame.bounds.top;
                    newSY = newSY - 3;
                    newSX = (newSY / sizeY) * sizeX;
                }

                // wider than column/page
		        if (newSX > colsize)
		        {
    				
			        newSX = colsize;
			        newSY = (newSX/sizeX)*sizeY;
		        }
                else if (width.Length > 0 && !fitToPage)
                {
                    if (type == Mode.SingleColumn)
                        newSX = colsize * (w / 800);
                    else
                        newSX = colsize * (w / 500);

                    newSY = (newSX / sizeX) * sizeY;
                }

                // higher than page
                if (newSY > frame.page.contentBounds.height)
                {
                    newSY = frame.page.contentBounds.height - 2;
                    newSX = (newSY / sizeY) * sizeX;
                }


                Rectangle rect = frame.GetRect();
                
                //Bounds newBounds = new Bounds(frame.GeometricBounds);
                frame.bounds.height = 300;
                frame.ApplyBounds();

                Bounds newBounds = new Bounds();
                newBounds.width = 10;
                newBounds.height = 10;
                rect.GeometricBounds = newBounds.raw;
    			
		        rect.FillColor = document.Swatches["None"];

                if (src.ToLower().IndexOf(".jpg") > 0 || border)
                    rect.StrokeWeight = 0.25;
                else
		            rect.StrokeWeight = 0;

                Bounds rectBounds = newBounds.Clone();

                if (type == Mode.SingleColumn)
                {
                    //rectBounds.left = (currentPage.contentBounds.width - newSX) / 2;
                    Paragraph p = (Paragraph)frame.textFrame.Paragraphs.FirstItem();
                    p.Justification = idJustification.idCenterAlign;

                }
                rectBounds.width = newSX;
                rectBounds.height = newSY;
                rect.GeometricBounds = rectBounds.raw;

                PlaceImage(filename, rect, true, crop, rotate);
                object img = rect.AllGraphics.FirstItem();
                

                frame.bounds.height = newSY + 1;
                frame.ApplyBounds();

                frame.ResizeAndPaginate();
    			
	        }
            classes.Add("img");
        }

        /*private static void SetPDFCrop(object tempimg)
        {
            if (tempimg is PDF)
            {
                idPDFCrop crop = ((PDF)tempimg).PDFAttributes.PDFCrop;
                ((PDF)tempimg).PDFAttributes.PDFCrop = idPDFCrop.idCropTrim;
            }
        }*/

        private Rectangle PlaceImageInRect(string filename, Bounds tempBounds, bool resize, idPDFCrop crop)
        {
            if (!File.Exists(filename))
            {
                Console.WriteLine("Image " + filename + " does not exist!");
                return null;
            }
           
             
            Rectangle temprect = currentPage.page.Rectangles.Add(miss, idLocationOptions.idAtEnd, miss);
            temprect.GeometricBounds = tempBounds.raw;
            temprect.StrokeWeight = 0;
            temprect.FillColor = document.Swatches["None"];


            PlaceImage(filename, temprect, resize, crop, false);



            return temprect;
        }

        private  void PlaceImage(string filename, Rectangle rect, bool resize, idPDFCrop crop, bool rotate)
        {
            if (!File.Exists(filename))
            {
                Console.WriteLine("Image " + filename + " does not exist!");
                return;
            }


            if (crop != null)
                application.PDFPlacePreferences.PDFCrop = crop;

            rect.Place(filename, false);

            object obj = rect.AllGraphics.LastItem();

            if (rotate)
            {
                if (obj is Image)
                {
                    ((Image)obj).RotationAngle = 90;
                }
                if (obj is PDF)
                {
                    ((PDF)obj).RotationAngle = 90;
                }
                    
            }

            if (resize)
                rect.Fit(idFitOptions.idProportionally);

            application.PDFPlacePreferences.PDFCrop = idPDFCrop.idCropContentAllLayers;
            //application.PDFPlacePreferences.PDFCrop = idPDFCrop..idCropContent;
        }

        private string GetAttachmentPath(string src)
        {
            string name = document.Name;
            name = name.Replace(".indd", "");
            name = name.Replace(" ", "_");

            string filename = Layout.basePath + "attachments\\" + name;
            if (!Directory.Exists(filename) && Directory.Exists(filename + "_bouldering") )
                filename += "_bouldering";
                
            filename +=  "\\" + src;
            return filename;
        }


        private string GetIconPath(string src)
        {
            string filename = Layout.basePath + "icons\\" + src;
            return filename;
        }


        string GetConfig(string key1, string key2)
        {
            return GetConfig(key1 + "." + key2);
        }

        string GetConfig(string key)
        {
            return Layout.GetConfig(key);
        }

        bool GetConfigBool(string key1, string key2)
        {
            return GetConfigBool(key1 + "." + key2);
        }


        bool GetConfigBool(string key)
        {
            string config = GetConfig(key);

            return (config != null && config.ToLower() == "true");

        }

        void ProcessHeader(XmlElement node)
        {
            string intro = GetAttr(node, "intro");
            string name = GetAttr(node, "name");
            string sun = GetAttr(node, "sun");
            string rock = GetAttr(node, "rock");
            string walk = GetAttr(node, "walk");
            string history = GetAttr(node, "history");
            string access = GetAttr(node, "access");
            string acknowledgement = GetAttr(node, "acknowledgement");

            GuideFrame frame = GetTextFrame(Mode.SingleColumn, FrameType.Multi, true);
            

            frame.AddPara( name, "heading1", true);
            frame.bounds.top -= 4;
            frame.ApplyBounds();
            frame.ResizeAndPaginate();
            frame.bottomOffset += 2;

            double bottomExtra = 0;
            double iconTop = currentPage.GetNextTop();

            bool done = false;
            
            string graphPath = GetAttachmentPath("graph.pdf");
            if (File.Exists(graphPath) && this.xml.SelectNodes("//climb").Count > 0)
            {
                Bounds graphBounds = new Bounds();
                graphBounds.top = iconTop;
                graphBounds.height = GRAPHSIZE;
                graphBounds.left = currentPage.contentBounds.left;
                graphBounds.width = GRAPHSIZE;
                //PlaceImageInRect( graphPath, graphBounds, true, idPDFCrop.idCropContent);
                PlaceImageInRect(graphPath, graphBounds, true, idPDFCrop.idCropContentAllLayers);

                bottomExtra = graphBounds.bottom + 3;

                done = true;
            }
            if (walk.Length > 0 || sun.Length > 0 || rock.Length > 0)
            {
                done = true;
                iconTop = DoHeaderIcon(iconTop, "walk2.pdf", walk);
                iconTop = DoHeaderIcon(iconTop, "sun2.pdf", sun);
                iconTop = DoHeaderIcon(iconTop, "rock2.pdf", rock);

                iconTop += 4;
            }

            if (done)
            {
                bottomExtra = Math.Max(iconTop, bottomExtra);
                currentPage.nextTopMin = bottomExtra;

                MasterSpread master = GetMaster("FP-Master");

                // assume every rect needs moving
                Rectangles rects = master.Rectangles;
                foreach (Rectangle rect in rects)
                {
                    Bounds b = new Bounds(rect.GeometricBounds);
                    b.bottom = bottomExtra - 2;
                    rect.GeometricBounds = b.raw;
                }
            }
            else
                frame.bottomOffset += 2;

            //DoIndentedText(walk, "Walk");
            //DoIndentedText(sun, "Sun");
            //DoIndentedText(rock, "Rock");

            
            

            DoIndentedText(acknowledgement, "Author");
            DoIndentedText(intro, "Intro");
            DoIndentedText(history, "History");
            DoIndentedText(access, "Access");

            currentPage.frames[currentPage.frames.Count - 1].bottomOffset += 1;
            //currentPage.currentY += 3;
        }

        private double DoHeaderIcon(double iconTop, string icon, string text)
        {
            if (text.Length > 0)
            {
                string iconPath = GetIconPath(icon);
                Bounds iconBounds = new Bounds();
                iconBounds.top = iconTop;
                iconBounds.left = currentPage.contentBounds.left + GRAPHSIZE + 2;
                double iconSize = ICONSIZE;
                iconBounds.width = iconSize;
                iconBounds.height = iconSize;

                //PlaceImageInRect(iconPath, iconBounds, true, idPDFCrop.idCropContent);
                PlaceImageInRect(iconPath, iconBounds, true, idPDFCrop.idCropContentAllLayers);

                GuideFrame right = GetTextFrame(Mode.SingleColumn, FrameType.Text, true);
                Bounds rightBounds = right.bounds;
                rightBounds.top = iconTop + 1;
                rightBounds.left = iconBounds.left + iconSize + 2;
                right.ApplyBounds();
                right.AddPara(text, "text", true);

                right.ResizeToFit();

                iconTop += Math.Max(iconSize, right.bounds.height) + 1;

            }
            return iconTop;
        }

        private void DoIndentedText(string text, string heading)
        {
            if (text.Length > 0)
            {
                text = HandleText(text);

                GuideFrame left = LeftHeader(heading);

                GuideFrame right = GetTextFrame(Mode.SingleColumn, FrameType.Text, true);
                Bounds rightBounds = right.bounds;

                rightBounds.top = left.bounds.top + 1;
                rightBounds.left += INDENTED + 1;
                right.ApplyBounds();
                right.AddPara(text, "text", true);

                right.ResizeToFit();

                if (right.OverflowsPage())
                {
                    if (right.page.contentBounds.bottom - right.bounds.top > 20)
                    {
                        GuidePage newPage = this.GetNextPage(right.page, true);

                        GuideFrame newFrame = right.SplitFrame(newPage);

                        newFrame.bounds.left = newFrame.page.contentBounds.left + INDENTED + 2;
                        //newFrame.TransformBoundsForNewPage(newPage);
                        newFrame.bounds.top = right.page.contentBounds.top;
                        newFrame.ApplyBounds();
                        newFrame.ResizeToFit();
                    }
                    else
                    {
                        //MoveFrameToNext(left, leftBounds, false);
                        left.MoveFrameToNext(false);

                        right.MoveToPage(currentPage);
                        right.bounds.top = left.bounds.top + 1;
                        right.ApplyBounds();
                        right.ResizeToFit();


                    }

                }
                else
                {
                    currentPage.nextTopMin = Math.Max(left.bounds.bottom + left.bottomOffset, right.bounds.bottom + left.bottomOffset);
                }
            }
        }

        private GuideFrame LeftHeader(string heading)
        {
            GuideFrame left = GetTextFrame(Mode.SingleColumn, FrameType.IndentedHeader, true);
            
            left.AddPara(heading, "indentedHeader", false);
            left.bounds.width = INDENTED;
            left.ApplyBounds();
            left.ResizeToFit();
            return left;
        }

        void ProcessGps(XmlElement node)
        {
            LeftHeader("GPS");

            GuideFrame frame = GetTextFrame(Mode.SingleColumn, FrameType.Multi, true);

            InsertionPoint ip = frame.lastInsertionPoint;

            Table table = ip.Tables.Add(idLocationOptions.idAtEnd, miss);
            table.ColumnCount = 6;
            

            XmlNodeList nl = node.SelectNodes("point");

            table.BodyRowCount = nl.Count + 1;

            Row header = (Row) table.Rows.FirstItem();
                
            header.RowType = idRowTypes.idHeaderRow;
           
            table.Width = 120;
            ((Cell)header.Cells[1]).Contents = "Code";
            ((Column)table.Columns[1]).Width = 13;
            
            ((Cell)header.Cells[2]).Contents = "Description";
            ((Column)table.Columns[2]).Width = 56;
            
            ((Cell)header.Cells[3]).Contents = "Zone";
            ((Column)table.Columns[3]).Width = 10;
            //((Cell)header.Cells[3]).Width = 8;
            ((Cell)header.Cells[4]).Contents = "Easting";
            ((Column)table.Columns[4]).Width = 14;
            ((Cell)header.Cells[5]).Contents = "Northing";
            ((Column)table.Columns[5]).Width = 14;
            ((Cell)header.Cells[6]).Contents = "Height";
            ((Column)table.Columns[6]).Width = 12;
            //((Cell)header.Cells[7]).Contents = "";
            //((Column)table.Columns[7]).Width = 16;




            ParagraphStyle ps = GetParaStyle("ClimbHeading");
            for (int i = 1; i <= 6; i++)
            {
            //    ((Paragraph)((Cell)header.Cells[i]).Paragraphs.FirstItem()).AppliedParagraphStyle = ps;
            }
            
            int rowindex = 2;
            foreach (XmlElement point in nl)
            {
                Row row = (Row)table.Rows[rowindex];

                ((Cell)row.Cells[1]).Contents = GetAttr(point, "code");
                ((Cell)row.Cells[2]).Contents = GetAttr(point, "description"); 
                ((Cell)row.Cells[3]).Contents = GetAttr(point, "zone"); 
                ((Cell)row.Cells[4]).Contents = GetAttr(point, "easting"); 
                ((Cell)row.Cells[5]).Contents = GetAttr(point, "northing");

                String h =  GetAttr(point, "height");

                if (h == "0")
                    h = "";

                ((Cell)row.Cells[6]).Contents = h;
                
                //((Cell)row.Cells[7]).Contents = "GDA94 UTM";

                rowindex++;
            }

            foreach (Cell cell in table.Cells)
            {
                cell.BottomEdgeStrokeWeight = 0.25;
                cell.TopEdgeStrokeWeight = 0.25;
                cell.LeftEdgeStrokeWeight = 0.25;
                cell.RightEdgeStrokeWeight = 0.25;
                if (cell.Paragraphs.Count > 0)
                    ((Paragraph)cell.Paragraphs.FirstItem()).AppliedParagraphStyle = document.ParagraphStyles["GPSText"];
            }

            foreach (Cell c in header.Cells)
            {
                c.FillColor = document.Swatches["Black"];
                ((Paragraph)c.Paragraphs.FirstItem()).AppliedParagraphStyle = document.ParagraphStyles["GPSHeader"];

            }

            frame.ResizeAndPaginate();
        }

        GuideFrame GetTextFrame(Mode mode, FrameType type, bool forceNew)
        {
            if (forceNew || currentPage.currentFrame == null || mode != currentPage.currentMode)
            {
                // create a new frame
                CloseCurrentFrame();
                currentPage.CreateTextFrame(mode, type);
            }

            return currentPage.currentFrame;

            /*
            if (mode == currentPage.currentMode && !forceNew)
            {
                if (currentPage.currentFrame == null)
                {
                    //global.currentFrame = createTextFrame(type, global.currentPage, global.currentY);
                    currentPage.CreateTextFrame(mode);
                }
                return currentPage.currentMode;
            }
            else
            {
                if (global.currentFrame != null)
                {
                    resizeToFit(global.currentFrame, false);

                    var newBounds = global.currentFrame.geometricBounds;
                    global.currentY = newBounds[2];
                }

                global.currentFrame = createTextFrame(type, global.currentPage, global.currentY);

                global.currentType = type;
                return global.currentFrame;
            }
             */
        }

        public GuidePage GetPage(int index)
        {
            if (index < pages.Count)
                return pages[index];

            for (int x = pages.Count; x <= index; x++)
            {
                Page p = document.Pages.Add(idLocationOptions.idAtEnd, miss);
                p.AppliedMaster = GetMaster("A-Master");
                pages.Add(new GuidePage(this, p, x));
            }

            return pages[index];
        }

        public GuidePage GetNextPage(GuidePage current, bool setCurrent)
        {
            int idx = pages.IndexOf(current);
            GuidePage ret = GetPage(idx + 1);

            if (setCurrent)
                currentPage = ret;

            return ret;
        }

        bool LoadXml()
        {
            string file = @"c:\guides\craglets\xml\" + document.Name.Replace(" ", "_").Replace(".indd",".xml");

            try
            {
                xml = new XmlDocument();
                xml.Load(file);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("Could not load xml for " + file);
                return false;

            }
            /*
            Links l = document.Links;
            foreach (Link link in l)
            {
                string linkType = link.LinkType;

                string fp = link.FilePath;

                if (linkType=="XML" || fp.ToLower().EndsWith(".xml") )
                {
                    Log("Loading xml: " + link.FilePath);
                    xml = new XmlDocument();
                    xml.Load( (string) link.FilePath);
                }
            }
             * */
        }

        void ClearPages()
        {

            // now set up list of current pages
            int i = 0;
            Pages pgs = document.Pages;
            foreach (Page page in pgs)
            {
                pages.Add( new GuidePage(this, page, i) );

                for (int tf = page.TextFrames.Count ; tf > 0; tf--)
                {

                    TextFrame textFrame = (TextFrame)page.TextFrames[tf];

                    bool del = true;

                    if (textFrame.Paragraphs.Count > 0)
                    {
                        Paragraph p = (Paragraph)textFrame.Paragraphs[1];
                        ParagraphStyle ps = (ParagraphStyle) p.AppliedParagraphStyle;
                        
                        if ( ps.Name == "hiddenToc0")
                            del = false;
                    }

                    if (del)
                        textFrame.Delete();
                }

                for (int tf = page.Rectangles.Count; tf > 0; tf--)
                {

                    Rectangle rect = (Rectangle)page.Rectangles[tf];
                    rect.Delete();
                }


                i++;

            }

        }

        void ClearEmptyPages()
        {
            for (int p = document.Pages.Count; p > 1; p--)
            {
                Page page = (Page) document.Pages[p];
                if (page != null && page.TextFrames.Count == 0 && page.AllGraphics.Count == 0)
                {
                    Log("Deleting page " + page.Name);
                    page.Delete();
                }
                else
                    break;
            }

            //foreach (GuidePage p in pages)
            //{
            //    if (p.page.TextFrames.Count == 0 && p.page.AllGraphics.Count == 0 && document.Pages.Count > 0 )
            //        p.page.Delete();
            //}
        }
       

        public void SaveAndClose()
        {
            if (document.Modified)
			{
                Log("Saving " + document.Name);
				document.Save(miss,false,miss,true);
			}
			
            /*
			Log("Generating PDF");
            PDFExportPreset preset = (PDFExportPreset) application.PDFExportPresets["Craglets"];
            
            string filename = "C:\\guides\\craglets\\" +  document.Name + ".pdf";

            document.Export(idExportFormat.idPDFType, filename, false, preset, miss, false);
			*/

            Close();
        }

        private void Close()
        {
            document.Close(idSaveOptions.idNo, miss, miss, false);
        }

        public bool UpdateLinks()
        {
            bool ret = false;

            Links lnks = document.Links;
            foreach (Link link in lnks)
            {
                if (link.Status==idLinkStatus.idLinkOutOfDate)
                {
                    Log("Should be Updating link to " + link.FilePath);

                    /*
                    string fp = link.FilePath;
                    if (fp.StartsWith("C:\\guides\\"))
                    {
                        fp = fp.Replace("C:\\guides\\", @"D:\Dropbox\My Dropbox\thesarvo\guides\");
                        
                        link.Relink(fp);
                    }
                    else

                        link.Update();

                    ret = true;
                     */
                }
            }

            return ret;
        }

        void Log(string message)
        {
            Console.WriteLine("  - " + message);
        }


        public ParagraphStyle GetParaStyle(string style)
        {
            ParagraphStyle styleObj = (ParagraphStyle)document.ParagraphStyles[style];
            return styleObj;
        }


        private void CloseCurrentFrame()
        {
            if (currentPage.currentFrame != null)
            {
                currentPage.currentFrame.ResizeAndPaginate();

                //ResizeToFit(currentPage.currentFrame);
                //currentPage.currentY = new Bounds(currentFrame.GeometricBounds).bottom;
            }
        }

        private void ManageSpread(bool swingingThird)
        {
            Log("Managing spread for " + document.Name );

            BookContent prevBook = (BookContent) application.ActiveBook.BookContents.PreviousItem(bookContent);
            string prevBookRange = prevBook.DocumentPageRange;
            // assume this is in the form xxx-yyy
            int lastPagePrev = Int32.Parse( prevBookRange.Split('-')[1] );
            
            bool lastPageOdd = (lastPagePrev % 2 == 1);

            int i = 1;
            int swingIndex = -1;
            Spread swingSpread = null;

            foreach (Spread s in document.Spreads)
            {
                if (s.AllowPageShuffle)
                {
                    swingIndex = i;
                    swingSpread = s;
                }
                i++;
            }

            if (!lastPageOdd)
            {
                // need a blank/swing page up front
                if (swingIndex != 1)
                {
                    if (swingSpread == null)
                    {
                        // insert a blank page
                        Log("Inserting black page before spread");
                        Page p = document.Pages.Add(idLocationOptions.idAtBeginning, miss);
                    }
                    else
                    {
                        // move swing to start
                        Log("Moving swing spread to start");
                        swingSpread.Move(idLocationOptions.idAtBeginning, document);
                    }
                }

            }
            else
            {
                // starts with even, check swing not first
                if (swingIndex == 1)
                {
                    // move the swing to end
                    Log("Moving swing spread to end");
                    swingSpread.Move(idLocationOptions.idAtEnd, document);
                }
            }

            ClearEmptyPages();
            SaveAndClose();
        }
    }
}
