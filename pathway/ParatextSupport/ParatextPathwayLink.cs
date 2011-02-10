﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Xsl;
using SIL.Tool;

namespace SIL.PublishingSolution
{
	public class ParatextPathwayLink
	{
		private Dictionary<string, object> m_xslParams;
        private string m_projectName;
        private string m_databaseName;
		private XslCompiledTransform m_cleanUsfx = new XslCompiledTransform();
		private XslCompiledTransform m_usfxToXhtml = new XslCompiledTransform();
		private XslCompiledTransform m_moveTitleSpansToTitle = new XslCompiledTransform();
		private XslCompiledTransform m_moveSpansToParas = new XslCompiledTransform();
		private XslCompiledTransform m_encloseParasInSections = new XslCompiledTransform();
		private XslCompiledTransform m_addImpliedSection = new XslCompiledTransform();
		private XslCompiledTransform m_encloseScrInColumns = new XslCompiledTransform();
		private XslCompiledTransform m_encloseSectionsInBook = new XslCompiledTransform();

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Initializes a new instance of the <see cref="ParatextPathwayLink"/> class.
		/// </summary>
		/// <param name="databaseName">Name of the database.</param>
		/// <param name="xslParams">The parameters from Paratext for the XSLT.</param>
		/// ------------------------------------------------------------------------------------
		public ParatextPathwayLink(string databaseName, Dictionary<string, object> xslParams)
		{
			m_databaseName = databaseName;
			m_xslParams = xslParams;

			// If the writing system is undefined or set (by default) to English, add a writing system code 
			// that should not have a dictionary to prevent all words from being marked as misspelled.
			object strWs;
			if (m_xslParams.TryGetValue("ws", out strWs))
			{
				if ((string)strWs == "en")
					m_xslParams["ws"] = "zxx";
			}
			else
			{
				Debug.Fail("Missing writing system parameter for XSLT");
				m_xslParams.Add("ws", "zxx");
			}
			object projObj;
			if (m_xslParams.TryGetValue("projName", out projObj))
				m_projectName = (string)projObj;
			LoadStyleSheets();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Initializes a new instance of the <see cref="ParatextPathwayLink"/> class.
		/// This method overload is included only for backward compatibility with earlier versions 
		/// of Paratext.
		/// </summary>
		/// <param name="projName">Name of the project (from scrText.Name)</param>
		/// <param name="databaseName">Name of the database.</param>
		/// <param name="ws">The writing system locale.</param>
		/// <param name="userWs">The user writing system locale.</param>
		/// <param name="userName">Name of the user.</param>
		/// ------------------------------------------------------------------------------------
		public ParatextPathwayLink(string projName, string databaseName, string ws, string userWs, string userName)
		{
			if (ws == "en")
				ws = "zxx";

		    m_projectName = projName;
			m_databaseName = databaseName;
		    Common.databaseName = databaseName;
			// Set parameters for the XSLT.
			m_xslParams = new Dictionary<string, object>();
			m_xslParams.Add("ws", ws);
			m_xslParams.Add("userWs", userWs);
			DateTime now = DateTime.Now;
			m_xslParams.Add("dateTime", now.Date);
			m_xslParams.Add("user", userName);
			m_xslParams.Add("projName", projName);

			LoadStyleSheets();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Loads the style sheets that are used to transform from Paratext USFX to XHTML.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void LoadStyleSheets()
		{
			// Create stylesheets
			m_cleanUsfx.Load(XmlReader.Create(
				Assembly.GetExecutingAssembly().GetManifestResourceStream(
				"ParatextSupport.XML_without_line_breaks.xsl")));
			m_usfxToXhtml.Load(XmlReader.Create(
				Assembly.GetExecutingAssembly().GetManifestResourceStream(
				"ParatextSupport.UsfxToXhtml.xsl")));
			m_moveTitleSpansToTitle.Load(XmlReader.Create(
				Assembly.GetExecutingAssembly().GetManifestResourceStream(
				"ParatextSupport.MoveTitleSpansToTitle.xsl")));
			m_moveSpansToParas.Load(XmlReader.Create(
				Assembly.GetExecutingAssembly().GetManifestResourceStream(
				"ParatextSupport.MoveSpansToParas.xsl")));
			m_encloseParasInSections.Load(XmlReader.Create(
				Assembly.GetExecutingAssembly().GetManifestResourceStream(
				"ParatextSupport.EncloseParasInSections.xsl")));
			m_addImpliedSection.Load(XmlReader.Create(
				Assembly.GetExecutingAssembly().GetManifestResourceStream(
				"ParatextSupport.AddImpliedSection.xsl")));
			m_encloseScrInColumns.Load(XmlReader.Create(
				Assembly.GetExecutingAssembly().GetManifestResourceStream(
				"ParatextSupport.EncloseScrInColumns.xsl")));
			m_encloseSectionsInBook.Load(XmlReader.Create(
				Assembly.GetExecutingAssembly().GetManifestResourceStream(
				"ParatextSupport.EncloseSectionsInBook.xsl")));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Exports to the current Scripture book to pathway.
		/// </summary>
		/// <param name="usfxDoc">The XML document representation of the USFM file.</param>
		/// ------------------------------------------------------------------------------------
		public void ExportToPathway(XmlDocument usfxDoc)
		{
            //// TestBed Code
            //// Save Paratext usfxDoc file.
            // usfxDoc.Save("d:\\usfxDoc.xml");
			if (string.IsNullOrEmpty(usfxDoc.InnerText))
			{
				// TODO: Localize string
				MessageBox.Show("The current book has no content to export.", string.Empty, MessageBoxButtons.OK);
				return;
			}
            ScriptureContents dlg = new ScriptureContents();
			dlg.DatabaseName = m_databaseName;
			DialogResult result = dlg.ShowDialog();
			if (result != DialogResult.Cancel)
			{
				string pubName = dlg.PublicationName;

				// Get the file name as set on the dialog.
				string outputLocationPath = dlg.OutputLocationPath;
                
                string cssFullPath = Path.Combine(outputLocationPath, pubName + ".css");
                StyToCSS styToCss = new StyToCSS();
                styToCss.ConvertStyToCSS(m_projectName, cssFullPath);
				string fileName = Path.Combine(outputLocationPath, pubName + ".xhtml");

				if (File.Exists(fileName))
				{
					// TODO: Localize string
					result = MessageBox.Show(string.Format("{0}" + Environment.NewLine +
						" already exists. Overwrite?", fileName), string.Empty,
					    MessageBoxButtons.YesNo);
					if (result == DialogResult.No)
						return;
				}

				ConvertUsfxToPathwayXhtmlFile(usfxDoc.InnerXml, fileName);

                PsExport exporter = new PsExport();
				exporter.DataType = "Scripture";
				exporter.Export(fileName);
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Exports to the specified Scripture books to pathway.
		/// </summary>
		/// <param name="usfxBooksToExport">The XML document representation of the Scripture 
		/// books in USFM file.</param>
		/// ------------------------------------------------------------------------------------
		public void ExportToPathway(List<XmlDocument> usfxBooksToExport)
		{
			// TODO: ProgressBar progressBar = new ProgressBar();
			ScriptureContents dlg = new ScriptureContents();
			dlg.DatabaseName = m_databaseName;
			DialogResult result = dlg.ShowDialog();
			if (result != DialogResult.Cancel)
			{
				string pubName = dlg.PublicationName;

				// Get the file name as set on the dialog.
				string outputLocationPath = dlg.OutputLocationPath;

				string cssFullPath = Path.Combine(outputLocationPath, pubName + ".css");
				StyToCSS styToCss = new StyToCSS();
				styToCss.ConvertStyToCSS(m_projectName, cssFullPath);
				string fileName = Path.Combine(outputLocationPath, pubName + ".xhtml");

				if (File.Exists(fileName))
				{
					// TODO: Localize string
					result = MessageBox.Show(string.Format("{0}" + Environment.NewLine +
						" already exists. Overwrite?", fileName), string.Empty,
						MessageBoxButtons.YesNo);
					if (result == DialogResult.No)
						return;
				}

				XmlDocument scrBooksDoc = CombineUsfxDocs(usfxBooksToExport);

				if (string.IsNullOrEmpty(scrBooksDoc.InnerText))
				{
					// TODO: Localize string
					MessageBox.Show("The current book has no content to export.", string.Empty, MessageBoxButtons.OK);
					return;
				}
				ConvertUsfxToPathwayXhtmlFile(scrBooksDoc.InnerXml, fileName);

				PsExport exporter = new PsExport();
				exporter.DataType = "Scripture";
				exporter.Export(fileName);
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Combines USFX of multiple books into a single XmlDocument.
		/// </summary>
		/// <param name="usfxBooksToExport">The Scripture books in USFX format to export.</param>
		/// <returns>a single XmlDocument containing all books.</returns>
		/// ------------------------------------------------------------------------------------
		private XmlDocument CombineUsfxDocs(List<XmlDocument> usfxBooksToExport)
		{
			Debug.Assert(usfxBooksToExport != null && usfxBooksToExport.Count > 0);

			XmlDocument allBooks = usfxBooksToExport[0];
			if (usfxBooksToExport.Count == 1)
				return allBooks;

			for (int iDoc = 1; iDoc < usfxBooksToExport.Count; iDoc++)
			{
				foreach (XmlNode nodeToAdd in usfxBooksToExport[iDoc].SelectSingleNode("/usfm").ChildNodes)
				{
					XmlNode prevNode = allBooks.SelectSingleNode("usfm").LastChild;
					XmlNode commonParent = prevNode.ParentNode;
					commonParent.InsertAfter(allBooks.ImportNode(nodeToAdd, true), prevNode);
				}
			}

			return allBooks;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Converts the USFX to an XHTML file for Pathway.
		/// </summary>
		/// <param name="usfx">The XML document representation of the USFM file.</param>
		/// <param name="fileName">file name with full path where xhtml file will be written</param>
		/// ------------------------------------------------------------------------------------
		private void ConvertUsfxToPathwayXhtmlFile(string usfx, string fileName)
		{
			// Create argument list
			XsltArgumentList args = new XsltArgumentList();
			//args.AddExtensionObject("urn:extensions", new XsltExtensions());
			// REVIEW: I removed the following line for this new location.
			//args.AddExtensionObject("urn:usfmextensions", new UsfmXsltExtensions());
			foreach (string paramName in m_xslParams.Keys)
				args.AddParam(paramName, "", m_xslParams[paramName]);

			// Step 0. Remove line breaks.
			StringBuilder cleanUsfx = new StringBuilder();
			XmlWriter htmlw0 = XmlWriter.Create(cleanUsfx, m_cleanUsfx.OutputSettings);
			m_cleanUsfx.Transform(XmlReader.Create(new StringReader(usfx)), null, htmlw0, null);

			// Step 1. Convert the SFMs to styles recognized by Pathway. 
			// (Also, move chapter numbers to the following paragraph, if they are not included there, in preparation for the next steps).
			StringBuilder html = new StringBuilder();
			XmlWriter htmlw = XmlWriter.Create(html, m_usfxToXhtml.OutputSettings);
			m_usfxToXhtml.Transform(XmlReader.Create(new StringReader(cleanUsfx.ToString())), args, htmlw, null);

			XmlReaderSettings settings = new XmlReaderSettings();
			settings.ProhibitDtd = false;

			// Step 2. Move title spans inside a paragraph.
			StringBuilder titleSpansToTitle = new StringBuilder();
			XmlWriter htmlw2 = XmlWriter.Create(titleSpansToTitle, m_moveTitleSpansToTitle.OutputSettings);
			XmlReader reader2 = XmlReader.Create(new StringReader(html.ToString()), settings);
			m_moveTitleSpansToTitle.Transform(reader2, null, htmlw2, null);

			// Step 3. Move any text outside a paragraph element into a paragraph.
			StringBuilder spansToParas = new StringBuilder();
			XmlWriter htmlw3 = XmlWriter.Create(spansToParas, m_moveSpansToParas.OutputSettings);
			XmlReader reader3 = XmlReader.Create(new StringReader(titleSpansToTitle.ToString()), settings);
			m_moveSpansToParas.Transform(reader3, null, htmlw3, null);

			// Step 4. Move paragraphs into appropriate section type (as determined by the paragraph styles).
			StringBuilder parasInSections = new StringBuilder();
			XmlWriter htmlw4 = XmlWriter.Create(parasInSections, m_encloseParasInSections.OutputSettings);
			XmlReader reader4 = XmlReader.Create(new StringReader(spansToParas.ToString()), settings);
			m_encloseParasInSections.Transform(reader4, null, htmlw4, null);

			//// Step 4.5. Add implied section (when there isn't an explicit section head between the intro and Scripture content)
			//StringBuilder implicitSectionAdded = new StringBuilder();
			//XmlWriter htmlw4_5 = XmlWriter.Create(implicitSectionAdded, m_addImpliedSection.OutputSettings);
			//XmlReader reader4_5 = XmlReader.Create(new StringReader(parasInSections.ToString()), settings);
			//m_addImpliedSection.Transform(reader4_5, null, htmlw4_5, null);

			// Step 5. Move Scripture sections into columns element.
			StringBuilder scrSectionsInColumns = new StringBuilder();
			XmlWriter htmlw5 = XmlWriter.Create(scrSectionsInColumns, m_encloseScrInColumns.OutputSettings);
			XmlReader reader5 = XmlReader.Create(new StringReader(parasInSections.ToString()), settings);
			m_encloseScrInColumns.Transform(reader5, null, htmlw5, null);

			// Step 6. Move all sections into scrBook element.
			FileStream xhtmlFile = new FileStream(fileName, FileMode.Create);
			XmlWriter htmlw6 = XmlWriter.Create(xhtmlFile, m_encloseSectionsInBook.OutputSettings);
			XmlReader reader6 = XmlReader.Create(new StringReader(scrSectionsInColumns.ToString()), settings);
			m_encloseSectionsInBook.Transform(reader6, null, htmlw6, null);
			xhtmlFile.Close();
		}
	}
}
