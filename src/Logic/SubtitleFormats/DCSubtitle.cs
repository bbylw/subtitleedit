﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace Nikse.SubtitleEdit.Logic.SubtitleFormats
{

// http://code.google.com/p/subtitleedit/issues/detail?id=18
//<?xml version="1.0" encoding="UTF-8"?>
//<DCSubtitle Version="1.0">
//  <SubtitleID>4EB245B8-4D3A-4158-9516-95DD20E8322E</SubtitleID>
//  <MovieTitle>Unknown</MovieTitle>
//  <ReelNumber>1</ReelNumber>
//  <Language>Swedish</Language>
//  <Font Italic="no">
//    <Subtitle SpotNumber="1" TimeIn="00:00:06:040" TimeOut="00:00:08:040" FadeUpTime="20" FadeDownTime="20">
//      <Text Direction="horizontal" HAlign="center" HPosition="0.0" VAlign="bottom" VPosition="6.0">DETTA HAR HÄNT...</Text>
//    </Subtitle>
//  </Font>
//</DCSubtitle>

    class DCSubtitle : SubtitleFormat
    {
        public override string Extension
        {
            get { return ".xml"; }
        }

        public override string Name
        {
            get { return "D-Cinema interop"; }
        }

        public override bool IsTimeBased
        {
            get { return true; }
        }

        public override bool IsMine(List<string> lines, string fileName)
        {
            var sb = new StringBuilder();
            lines.ForEach(line => sb.AppendLine(line));
            string xmlAsString = sb.ToString().Trim();
            if (xmlAsString.Contains("<DCSubtitle"))
            {
                var xml = new XmlDocument();
                try
                {
                    xml.LoadXml(xmlAsString);

                    var subtitles = xml.DocumentElement.SelectNodes("//Subtitle");
                    if (subtitles != null)
                        return subtitles != null && subtitles.Count > 0;
                    return false;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        private static string RemoveSubStationAlphaFormatting(string s)
        {
            int indexOfBegin = s.IndexOf("{", StringComparison.Ordinal);
            while (indexOfBegin >= 0 && s.IndexOf("}", StringComparison.Ordinal) > indexOfBegin)
            {
                int indexOfEnd = s.IndexOf("}", StringComparison.Ordinal);
                s = s.Remove(indexOfBegin, (indexOfEnd - indexOfBegin) + 1);
                indexOfBegin = s.IndexOf("{", StringComparison.Ordinal);
            }
            return s;
        }

        public override string ToText(Subtitle subtitle, string title)
        {
            string languageEnglishName;
            try
            {
                string languageShortName = Utilities.AutoDetectGoogleLanguage(subtitle);
                var ci = CultureInfo.CreateSpecificCulture(languageShortName);
                languageEnglishName = ci.EnglishName;
                int indexOfStartP = languageEnglishName.IndexOf("(");
                if (indexOfStartP > 1)
                    languageEnglishName = languageEnglishName.Remove(indexOfStartP).Trim();
            }
            catch
            {
                languageEnglishName = "English";
            }

            string xmlStructure = "<DCSubtitle Version=\"1.0\">" + Environment.NewLine +
                                    "    <SubtitleID>" + "4EB245B8-4D3A-4158-9516-95DD20E8322E".ToLower() + "</SubtitleID>" + Environment.NewLine +
                                    "    <MovieTitle></MovieTitle>" + Environment.NewLine +
                                    "    <ReelNumber>1</ReelNumber>" + Environment.NewLine +
                                    "    <Language>" + languageEnglishName + "</Language>" + Environment.NewLine +
                                    "    <LoadFont URI=\"" + Configuration.Settings.SubtitleSettings.DCinemaFontFile + "\" Id=\"Font1\"/>" + Environment.NewLine +
                                    "    <Font Id=\"Font1\" Color=\"FFFFFFFF\" Effect=\"border\" EffectColor=\"FF000000\" Italic=\"no\" Underlined=\"no\" Script=\"normal\" Size=\"42\">" + Environment.NewLine +
                                    "    </Font>" + Environment.NewLine +
                                    "</DCSubtitle>";

            XmlDocument xml = new XmlDocument();
            xml.LoadXml(xmlStructure);

            var ss = Configuration.Settings.SubtitleSettings;
            string loadedFontId = "Font1";
            if (!string.IsNullOrEmpty(ss.CurrentDCinemaFontId))
                loadedFontId = ss.CurrentDCinemaFontId;

            if (string.IsNullOrEmpty(ss.CurrentDCinemaMovieTitle))
                ss.CurrentDCinemaMovieTitle = title;

            if (ss.CurrentDCinemaFontSize == 0 || string.IsNullOrEmpty(ss.CurrentDCinemaFontEffect))
                Configuration.Settings.SubtitleSettings.InitializeDCinameSettings(true);

            xml.DocumentElement.SelectSingleNode("MovieTitle").InnerText = ss.CurrentDCinemaMovieTitle;
            xml.DocumentElement.SelectSingleNode("SubtitleID").InnerText = ss.CurrentDCinemaSubtitleId;
            xml.DocumentElement.SelectSingleNode("ReelNumber").InnerText = ss.CurrentDCinemaReelNumber;
            xml.DocumentElement.SelectSingleNode("Language").InnerText = ss.CurrentDCinemaLanguage;
            xml.DocumentElement.SelectSingleNode("LoadFont").Attributes["URI"].InnerText = ss.CurrentDCinemaFontUri;
            xml.DocumentElement.SelectSingleNode("LoadFont").Attributes["Id"].InnerText = loadedFontId;
            int fontSize = ss.CurrentDCinemaFontSize;
            xml.DocumentElement.SelectSingleNode("Font").Attributes["Id"].InnerText = loadedFontId;
            xml.DocumentElement.SelectSingleNode("Font").Attributes["Color"].InnerText = "FF" + Utilities.ColorToHex(ss.CurrentDCinemaFontColor).TrimStart('#').ToUpper();
            xml.DocumentElement.SelectSingleNode("Font").Attributes["Effect"].InnerText = ss.CurrentDCinemaFontEffect;
            xml.DocumentElement.SelectSingleNode("Font").Attributes["EffectColor"].InnerText = "FF" + Utilities.ColorToHex(ss.CurrentDCinemaFontEffectColor).TrimStart('#').ToUpper();
            xml.DocumentElement.SelectSingleNode("Font").Attributes["Size"].InnerText = ss.CurrentDCinemaFontSize.ToString();

            XmlNode mainListFont = xml.DocumentElement.SelectSingleNode("Font");
            int no = 0;
            foreach (Paragraph p in subtitle.Paragraphs)
            {
                if (!string.IsNullOrEmpty(p.Text))
                {
                    XmlNode subNode = xml.CreateElement("Subtitle");

                    XmlAttribute id = xml.CreateAttribute("SpotNumber");
                    id.InnerText = (no + 1).ToString();
                    subNode.Attributes.Append(id);

                    XmlAttribute fadeUpTime = xml.CreateAttribute("FadeUpTime");
                    fadeUpTime.InnerText = Configuration.Settings.SubtitleSettings.DCinemaFadeUpDownTime.ToString();
                    subNode.Attributes.Append(fadeUpTime);

                    XmlAttribute fadeDownTime = xml.CreateAttribute("FadeDownTime");
                    fadeDownTime.InnerText = Configuration.Settings.SubtitleSettings.DCinemaFadeUpDownTime.ToString();
                    subNode.Attributes.Append(fadeDownTime);

                    XmlAttribute start = xml.CreateAttribute("TimeIn");
                    start.InnerText = ConvertToTimeString(p.StartTime);
                    subNode.Attributes.Append(start);

                    XmlAttribute end = xml.CreateAttribute("TimeOut");
                    end.InnerText = ConvertToTimeString(p.EndTime);
                    subNode.Attributes.Append(end);


                    bool alignLeft = p.Text.StartsWith("{\\a1}") || p.Text.StartsWith("{\\a5}") || p.Text.StartsWith("{\\a9}") || // sub station alpha
                                    p.Text.StartsWith("{\\an1}") || p.Text.StartsWith("{\\an4}") || p.Text.StartsWith("{\\an7}"); // advanced sub station alpha

                    bool alignRight = p.Text.StartsWith("{\\a3}") || p.Text.StartsWith("{\\a7}") || p.Text.StartsWith("{\\a11}") || // sub station alpha
                                      p.Text.StartsWith("{\\an3}") || p.Text.StartsWith("{\\an6}") || p.Text.StartsWith("{\\an9}"); // advanced sub station alpha

                    bool alignVTop = p.Text.StartsWith("{\\a5}") || p.Text.StartsWith("{\\a6}") || p.Text.StartsWith("{\\a7}") || // sub station alpha
                                    p.Text.StartsWith("{\\an7}") || p.Text.StartsWith("{\\an8}") || p.Text.StartsWith("{\\an9}"); // advanced sub station alpha

                    bool alignVCenter = p.Text.StartsWith("{\\a9}") || p.Text.StartsWith("{\\a10}") || p.Text.StartsWith("{\\a11}") || // sub station alpha
                                      p.Text.StartsWith("{\\an4}") || p.Text.StartsWith("{\\an5}") || p.Text.StartsWith("{\\an6}"); // advanced sub station alpha

                    // remove styles for display text (except italic)
                    string text = RemoveSubStationAlphaFormatting(p.Text);


                    string[] lines = text.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    int vPos = 1 + lines.Length * 7;
                    int vPosFactor = (int)Math.Round(fontSize / 7.4);
                    if (alignVTop)
                    {
                        vPos = Configuration.Settings.SubtitleSettings.DCinemaBottomMargin; // Bottom margin is normally 8
                    }
                    else if (alignVCenter)
                    {
                        vPos = (int)Math.Round((lines.Length * vPosFactor * -1) / 2.0);
                    }
                    else
                    {
                        vPos = (lines.Length * vPosFactor) - vPosFactor + Configuration.Settings.SubtitleSettings.DCinemaBottomMargin; // Bottom margin is normally 8
                    }

                    bool isItalic = false;
                    int fontNo = 0;
                    System.Collections.Generic.Stack<string> fontColors = new Stack<string>();
                    foreach (string line in lines)
                    {
                        XmlNode textNode = xml.CreateElement("Text");

                        XmlAttribute vPosition = xml.CreateAttribute("VPosition");
                        vPosition.InnerText = vPos.ToString();
                        textNode.Attributes.Append(vPosition);

                        XmlAttribute vAlign = xml.CreateAttribute("VAlign");
                        if (alignVTop)
                            vAlign.InnerText = "top";
                        else if (alignVCenter)
                            vAlign.InnerText = "center";
                        else
                            vAlign.InnerText = "bottom";
                        textNode.Attributes.Append(vAlign);

                        XmlAttribute hAlign = xml.CreateAttribute("HAlign");
                        if (alignLeft)
                            hAlign.InnerText = "left";
                        else if (alignRight)
                            hAlign.InnerText = "right";
                        else
                            hAlign.InnerText = "center";
                        textNode.Attributes.Append(hAlign);

                        XmlAttribute direction = xml.CreateAttribute("Direction");
                        direction.InnerText = "horizontal";
                        textNode.Attributes.Append(direction);

                        int i = 0;
                        var txt = new StringBuilder();
                        var html = new StringBuilder();
                        XmlNode nodeTemp = xml.CreateElement("temp");
                        while (i < line.Length)
                        {
                            if (!isItalic && line.Substring(i).StartsWith("<i>"))
                            {
                                if (txt.Length > 0)
                                {
                                    nodeTemp.InnerText = txt.ToString();
                                    html.Append(nodeTemp.InnerXml);
                                    txt = new StringBuilder();
                                }
                                isItalic = true;
                                i += 2;
                            }
                            else if (isItalic && line.Substring(i).StartsWith("</i>"))
                            {
                                if (txt.Length > 0)
                                {
                                    XmlNode fontNode = xml.CreateElement("Font");

                                    XmlAttribute italic = xml.CreateAttribute("Italic");
                                    italic.InnerText = "yes";
                                    fontNode.Attributes.Append(italic);

                                    if (line.Length > i + 5 && line.Substring(i + 4).StartsWith("</font>"))
                                    {
                                        XmlAttribute fontColor = xml.CreateAttribute("Color");
                                        fontColor.InnerText = fontColors.Pop();
                                        fontNode.Attributes.Append(fontColor);
                                        fontNo--;
                                        i += 7;
                                    }

                                    fontNode.InnerText = Utilities.RemoveHtmlTags(txt.ToString());
                                    html.Append(fontNode.OuterXml);
                                    txt = new StringBuilder();
                                }
                                isItalic = false;
                                i += 3;
                            }
                            else if (line.Substring(i).StartsWith("<font color=") && line.Substring(i+3).Contains(">"))
                            {
                                int endOfFont = line.IndexOf(">", i);
                                if (txt.Length > 0)
                                {
                                    nodeTemp.InnerText = txt.ToString();
                                    html.Append(nodeTemp.InnerXml);
                                    txt = new StringBuilder();
                                }
                                string c = line.Substring(i + 12, endOfFont - (i + 12));
                                c = c.Trim('"').Trim('\'').Trim();
                                if (c.StartsWith("#"))
                                    c = c.TrimStart('#').ToUpper().PadLeft(8, 'F');
                                fontColors.Push(c);
                                fontNo++;
                                i += endOfFont - i;
                            }
                            else if (fontNo > 0 && line.Substring(i).StartsWith("</font>"))
                            {
                                if (txt.Length > 0)
                                {
                                    XmlNode fontNode = xml.CreateElement("Font");

                                    XmlAttribute fontColor = xml.CreateAttribute("Color");
                                    fontColor.InnerText = fontColors.Pop();
                                    fontNode.Attributes.Append(fontColor);

                                    if (line.Length > i + 9 && line.Substring(i + 7).StartsWith("</i>"))
                                    {
                                        XmlAttribute italic = xml.CreateAttribute("Italic");
                                        italic.InnerText = "yes";
                                        fontNode.Attributes.Append(italic);
                                        isItalic = false;
                                        i += 4;
                                    }

                                    fontNode.InnerText = Utilities.RemoveHtmlTags(txt.ToString());
                                    html.Append(fontNode.OuterXml);
                                    txt = new StringBuilder();
                                }
                                fontNo--;
                                i += 6;
                            }
                            else
                            {
                                txt.Append(line.Substring(i, 1));
                            }
                            i++;
                        }
                        if (isItalic)
                        {
                            if (txt.Length > 0)
                            {
                                XmlNode fontNode = xml.CreateElement("Font");

                                XmlAttribute italic = xml.CreateAttribute("Italic");
                                italic.InnerText = "yes";
                                fontNode.Attributes.Append(italic);

                                fontNode.InnerText = Utilities.RemoveHtmlTags(line);
                                html.Append(fontNode.OuterXml);
                            }
                        }
                        else
                        {
                            if (txt.Length > 0)
                            {
                                nodeTemp.InnerText = txt.ToString();
                                html.Append(nodeTemp.InnerXml);
                            }
                        }
                        textNode.InnerXml = html.ToString();

                        subNode.AppendChild(textNode);
                        vPos -= vPosFactor;
                    }

                    mainListFont.AppendChild(subNode);
                    no++;
                }
            }

            var ms = new MemoryStream();
            var writer = new XmlTextWriter(ms, Encoding.UTF8);
            writer.Formatting = Formatting.Indented;
            xml.Save(writer);
            return Encoding.UTF8.GetString(ms.ToArray()).Trim().Replace("encoding=\"utf-8\"", "encoding=\"UTF-8\"");
        }

        public override void LoadSubtitle(Subtitle subtitle, List<string> lines, string fileName)
        {
            _errorCount = 0;

            var sb = new StringBuilder();
            lines.ForEach(line => sb.AppendLine(line));
            var xml = new XmlDocument();
            xml.LoadXml(sb.ToString());

            var ss = Configuration.Settings.SubtitleSettings;
            try
            {
                ss.InitializeDCinameSettings(false);
                XmlNode node = xml.DocumentElement.SelectSingleNode("SubtitleID");
                if (node != null)
                    ss.CurrentDCinemaSubtitleId = node.InnerText;

                node = xml.DocumentElement.SelectSingleNode("ReelNumber");
                if (node != null)
                    ss.CurrentDCinemaReelNumber = node.InnerText;

                node = xml.DocumentElement.SelectSingleNode("Language");
                if (node != null)
                    ss.CurrentDCinemaLanguage = node.InnerText;

                node = xml.DocumentElement.SelectSingleNode("MovieTitle");
                if (node != null)
                    ss.CurrentDCinemaMovieTitle = node.InnerText;

                node = xml.DocumentElement.SelectSingleNode("Font");
                if (node != null)
                {
                    ss.CurrentDCinemaFontUri = node.InnerText;
                    if (node.Attributes["ID"] != null)
                        ss.CurrentDCinemaFontId = node.Attributes["ID"].InnerText;
                    if (node.Attributes["Size"] != null)
                        ss.CurrentDCinemaFontSize = Convert.ToInt32(node.Attributes["Size"].InnerText);
                    if (node.Attributes["Color"] != null)
                        ss.CurrentDCinemaFontColor = System.Drawing.ColorTranslator.FromHtml("#" + node.Attributes["Color"].InnerText);
                    if (node.Attributes["Effect"] != null)
                        ss.CurrentDCinemaFontEffect = node.Attributes["Effect"].InnerText;
                    if (node.Attributes["EffectColor"] != null)
                        ss.CurrentDCinemaFontEffectColor = System.Drawing.ColorTranslator.FromHtml("#" + node.Attributes["EffectColor"].InnerText);
                }
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine(exception.Message);
            }

            foreach (XmlNode node in xml.DocumentElement.SelectNodes("//Subtitle"))
            {
                try
                {
                    var pText = new StringBuilder();
                    string lastVPosition = string.Empty;
                    foreach (XmlNode innerNode in node.ChildNodes)
                    {
                        switch (innerNode.Name.ToString())
                        {
                            case "Text":
                                if (innerNode.Attributes["VPosition"] != null)
                                {
                                    string vPosition = innerNode.Attributes["VPosition"].InnerText;
                                    if (vPosition != lastVPosition)
                                    {
                                        if (pText.Length > 0 && lastVPosition != string.Empty)
                                            pText.AppendLine();
                                        lastVPosition = vPosition;
                                    }
                                }
                                 bool alignLeft = false;
                                bool alignRight = false;
                                bool alignVTop = false;
                                bool alignVCenter = false;
                                if (innerNode.Attributes["HAlign"] != null)
                                {
                                    string hAlign = innerNode.Attributes["HAlign"].InnerText;
                                    if (hAlign == "left")
                                        alignLeft = true;
                                    else if (hAlign == "right")
                                        alignRight = true;
                                }
                                if (innerNode.Attributes["VAlign"] != null)
                                {
                                    string hAlign = innerNode.Attributes["VAlign"].InnerText;
                                    if (hAlign == "top")
                                        alignVTop = true;
                                    else if (hAlign == "center")
                                        alignVCenter = true;
                                }
                                if (alignLeft || alignRight || alignVCenter || alignVTop)
                                {
                                    if (!pText.ToString().StartsWith("{\\an"))
                                    {
                                        string pre = string.Empty;
                                        if (alignVTop)
                                        {
                                            if (alignLeft)
                                                pre = "{\\an7}";
                                            else if (alignRight)
                                                pre = "{\\an9}";
                                            else
                                                pre = "{\\an8}";
                                        }
                                        else if (alignVCenter)
                                        {
                                            if (alignLeft)
                                                pre = "{\\an4}";
                                            else if (alignRight)
                                                pre = "{\\an6}";
                                            else
                                                pre = "{\\an5}";
                                        }
                                        else
                                        {
                                            if (alignLeft)
                                                pre = "{\\an1}";
                                            else if (alignRight)
                                                pre = "{\\an3}";
                                        }
                                        string temp = pre + pText.ToString();
                                        pText = new StringBuilder();
                                        pText.Append(temp);
                                    }
                                }

                                if (innerNode.ChildNodes.Count == 0)
                                {
                                    pText.Append(innerNode.InnerText);
                                }
                                else
                                {
                                    foreach (XmlNode innerInnerNode in innerNode)
                                    {
                                        if (innerInnerNode.Name == "Font" && innerInnerNode.Attributes["Italic"] != null &&
                                           innerInnerNode.Attributes["Italic"].InnerText.ToLower() == "yes")
                                        {
                                            if (innerInnerNode.Attributes["Color"] != null)
                                                pText.Append("<i><font color=\"" + GetColorStringFromDCinema(innerInnerNode.Attributes["Color"].Value) + "\">" + innerInnerNode.InnerText + "</font><i>");
                                            else
                                                pText.Append("<i>" + innerInnerNode.InnerText + "</i>");
                                        }
                                        else if (innerInnerNode.Name == "Font" && innerInnerNode.Attributes["Color"] != null)
                                        {
                                            if (innerInnerNode.Attributes["Italic"] != null && innerInnerNode.Attributes["Italic"].InnerText.ToLower() == "yes")
                                                pText.Append("<i><font color=\"" + GetColorStringFromDCinema(innerInnerNode.Attributes["Color"].Value) + "\">" + innerInnerNode.InnerText + "</font><i>");
                                            else
                                                pText.Append("<font color=\"" + GetColorStringFromDCinema(innerInnerNode.Attributes["Color"].Value) + "\">" + innerInnerNode.InnerText + "</font>");
                                        }
                                        else
                                        {
                                            pText.Append(innerInnerNode.InnerText);
                                        }
                                    }
                                }
                                break;
                            default:
                                pText.Append(innerNode.InnerText);
                                break;
                        }
                    }
                    string start = node.Attributes["TimeIn"].InnerText;
                    string end = node.Attributes["TimeOut"].InnerText;

                    subtitle.Paragraphs.Add(new Paragraph(GetTimeCode(start), GetTimeCode(end), pText.ToString()));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    _errorCount++;
                }
            }

            if (subtitle.Paragraphs.Count > 0)
                subtitle.Header = xml.OuterXml; // save id/language/font for later use

            subtitle.Renumber(1);
        }

        private string GetColorStringForDCinema(string p)
        {
            string s = p.ToUpper().Trim();
            if (s.Replace("#", string.Empty).
                Replace("0", string.Empty).
                Replace("1", string.Empty).
                Replace("2", string.Empty).
                Replace("3", string.Empty).
                Replace("4", string.Empty).
                Replace("5", string.Empty).
                Replace("6", string.Empty).
                Replace("7", string.Empty).
                Replace("8", string.Empty).
                Replace("9", string.Empty).
                Replace("A", string.Empty).
                Replace("B", string.Empty).
                Replace("C", string.Empty).
                Replace("D", string.Empty).
                Replace("E", string.Empty).
                Replace("F", string.Empty).Length == 0)
            {
                return s.TrimStart('#');
            }
            else
            {
                return p;
            }
        }

        private string GetColorStringFromDCinema(string p)
        {
            string s = p.ToLower().Trim();
            if (s.Replace("#", string.Empty).
                Replace("0", string.Empty).
                Replace("1", string.Empty).
                Replace("2", string.Empty).
                Replace("3", string.Empty).
                Replace("4", string.Empty).
                Replace("5", string.Empty).
                Replace("6", string.Empty).
                Replace("7", string.Empty).
                Replace("8", string.Empty).
                Replace("9", string.Empty).
                Replace("a", string.Empty).
                Replace("b", string.Empty).
                Replace("c", string.Empty).
                Replace("d", string.Empty).
                Replace("e", string.Empty).
                Replace("f", string.Empty).Length == 0)
            {
                if (s.StartsWith("#"))
                    return s;
                else
                    return "#" + s;
            }
            else
            {
                return p;
            }
        }

        private static TimeCode GetTimeCode(string s)
        {
            string[] parts = s.Split(new char[] { ':', '.', ',' });

            int milliseconds = (int)(int.Parse(parts[3]) * 4); // 000 to 249
            if (s.Contains("."))
                milliseconds = (int)(Math.Round((int.Parse(parts[3]) / 10.0 * 1000.0)));
            if (milliseconds > 999)
                milliseconds = 999;

            var ts = new TimeSpan(0, int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), milliseconds);
            return new TimeCode(ts);
        }

        private static string ConvertToTimeString(TimeCode time)
        {
            return string.Format("{0:00}:{1:00}:{2:00}:{3:000}", time.Hours, time.Minutes, time.Seconds, time.Milliseconds / 4);
        }

    }
}


