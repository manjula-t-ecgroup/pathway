﻿// --------------------------------------------------------------------------------------
// <copyright file="Exportepub.cs" from='2009' to='2014' company='SIL International'>
//      Copyright (C) 2014, SIL International. All Rights Reserved.
//
//      Distributable under the terms of either the Common Public License or the
//      GNU Lesser General Public License, as specified in the LICENSING.txt file.
// </copyright>
// <author>Erik Brommers</author>
// <email>erik_brommers@sil.org</email>
// Last reviewed:
// 
// <remarks>
// epub export
//
// .epub files are zipped archives with the following file structure:
// |-mimetype
// |-META-INF
// | `-container.xml
// |-OEBPS
//   |-content.opf
//   |-toc.ncx
//   |-<any fonts and other files embedded into the archive>
//   |-<list of files in book (C) xhtml format + .css for styling>
//   '-<any images referenced in book files>
//
// See also http://www.openebook.org/2007/ops/OPS_2.0_final_spec.html
// </remarks>
// --------------------------------------------------------------------------------------

// uncomment this to write out performance timings
//#define TIME_IT 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Xsl;
using epubConvert;
using epubConvert.Properties;
using epubValidator;
using SIL.Tool;


namespace SIL.PublishingSolution
{
    /// <summary>
    /// Possible values for the MissingFont and NonSilFont properties.
    /// </summary>
    public enum FontHandling
    {
        EmbedFont,
        SubstituteDefaultFont,
        PromptUser,
        CancelExport
    }

    public class Exportepub : IExportProcess
    {

        private Dictionary<string, EmbeddedFont> _embeddedFonts;  // font information for this export
        private Dictionary<string, string> _langFontDictionary; // languages and font names in use for this export
        private readonly XslCompiledTransform _addDicTocHeads = new XslCompiledTransform();
        private readonly XslCompiledTransform _fixEpubToc = new XslCompiledTransform();

        private string _currentChapterNumber = string.Empty;
        private bool _isIncludeImage = true;

        private bool _isNoteTargetReferenceExists;
        private bool _isUnixOs;

        //        protected static PostscriptLanguage _postscriptLanguage = new PostscriptLanguage();
        protected string InputType = "dictionary";

        public const string ReferencesFilename = "zzReferences.xhtml";


        // property implementations
        public string Title { get; set; }
        public string Creator { get; set; }
        public string Description { get; set; }
        public string Publisher { get; set; }
        public string Coverage { get; set; }
        public string Rights { get; set; }
        public string Format { get; set; }
        public string Source { get; set; }
        public bool EmbedFonts { get; set; }
        public bool IncludeFontVariants { get; set; }
        public string TocLevel { get; set; }
        public int MaxImageWidth { get; set; }

        public int BaseFontSize { get; set; }
        public int DefaultLineHeight { get; set; }
        /// <summary>
        /// Fallback font (if the embedded font is missing or non-SIL)
        /// </summary>
        public string DefaultFont { get; set; }
        public string DefaultAlignment { get; set; }
        public string ChapterNumbers { get; set; }
        public string References { get; set; }
        public FontHandling MissingFont { get; set; } // note that this doesn't use all the enum values
        public FontHandling NonSilFont { get; set; }
        private ArrayList PseudoClass = new ArrayList();
        public bool PageBreak;
        private readonly Dictionary<string, string> _tocIdMapping = new Dictionary<string, string>();
        readonly List<string> _tocIDs = new List<string>();

        // interface methods
        public string ExportType
        {
            get
            {
                return "E-Book (.epub)";
            }
        }

        /// <summary>
        /// Returns what input data types this export process handles. The epub exporter
        /// currently handles scripture and dictionary data types.
        /// </summary>
        /// <param name="inputDataType">input data type to test</param>
        /// <returns>true if this export process handles the specified data type</returns>
        public bool Handle(string inputDataType)
        {
            var returnValue = inputDataType.ToLower() == "dictionary" || inputDataType.ToLower() == "scripture";
            return returnValue;
        }

        /// <summary>
        /// Entry point for epub converter
        /// </summary>
        /// <param name="projInfo">values passed including xhtml and css names</param>
        /// <returns>true if succeeds</returns>
        public bool Export(PublicationInformation projInfo)
        {
            if (projInfo == null)
                return false;
            const bool success = true;

            #region Set up progress reporting
#if (TIME_IT)
            DateTime dt1 = DateTime.Now;    // time this thing
#endif
            var myCursor = UseWaitCursor();
            var curdir = Environment.CurrentDirectory;
            var inProcess = SetupProgressReporting(20);
            #endregion Set up progress reporting

            #region Setup
            inProcess.SetStatus("Setup");
            var bookId = Guid.NewGuid(); // NOTE: this creates a new ID each time Pathway is run. 
            PageBreak = InputType.ToLower() == "dictionary" && GetPageBreakStatus(projInfo.SelectedTemplateStyle);
            #region LoadXslts
            var addRevId = LoadAddRevIdXslt();
            var noXmlSpace = LoadNoXmlSpaceXslt();
            var fixEpub = LoadFixEpubXslt();
            _addDicTocHeads.Load(XmlReader.Create(Common.UsersXsl("addDicTocHeads.xsl")));
            _fixEpubToc.Load(XmlReader.Create(Common.UsersXsl("FixEpubToc.xsl")));
            #endregion
            
            var preProcessor = new PreExportProcess(projInfo);
            preProcessor.RemoveBrokenImage();

            _isUnixOs = Common.UnixVersionCheck();
            if (_isUnixOs)
            {
                Common.RemoveDTDForLinuxProcess(projInfo.DefaultXhtmlFileWithPath);
            }

            _isIncludeImage = GetIncludeImageStatus(projInfo.SelectedTemplateStyle);

            _isNoteTargetReferenceExists = Common.NodeExists(projInfo.DefaultXhtmlFileWithPath, "");

            LoadPropertiesFromSettings();
            inProcess.PerformStep();
            #endregion Setup

            #region Xhtml preprocessing
            inProcess.SetStatus("Preprocessing content");
            InsertBeforeAfterInXhtml(projInfo);
            var outputFolder = SetOutputFolderAndCurrentDirectory(projInfo);
            Common.SetProgressBarValue(projInfo.ProgressBar, projInfo.DefaultXhtmlFileWithPath);
            XhtmlPreprocessing(projInfo, preProcessor);
            var langArray = InitializeLangArray(projInfo);
            inProcess.PerformStep();
            #endregion Xhtml preprocessing

            #region Css preprocessing
            inProcess.SetStatus("Preprocessing stylesheet");
            var cssFullPath = CssFullPath(projInfo);
            Common.WriteDefaultLanguages(projInfo, cssFullPath);
            var tempFolder = Path.GetDirectoryName(preProcessor.ProcessedXhtml);
            var mergedCss = MergeAndFilterCss(preProcessor, tempFolder, cssFullPath);
            /* Modify the content in css file for before after css style process */
            preProcessor.InsertPseudoContentProperty(mergedCss, PseudoClass);
            CustomizeCss(mergedCss);
            var niceNameCss = NiceNameCss(projInfo, tempFolder, ref mergedCss);
            var defaultCss = Path.GetFileName(niceNameCss);
            Common.SetDefaultCSS(projInfo.DefaultXhtmlFileWithPath, defaultCss);
            Common.SetDefaultCSS(preProcessor.ProcessedXhtml, defaultCss);
            inProcess.PerformStep();
            #endregion Css preprocessing
            
            #region Hacks
            XhtmlNamespaceHack(projInfo, preProcessor, langArray);
            Common.ApplyXslt(preProcessor.ProcessedXhtml, noXmlSpace);
            Common.ApplyXslt(preProcessor.ProcessedXhtml, fixEpub);
            inProcess.PerformStep();
            #endregion Hacks

            #region Adding Navigation and Front Matter
            inProcess.SetStatus("Adding Navigation");
            preProcessor.PrepareBookNameAndChapterCount();
            inProcess.PerformStep();

            // insert the front matter items as separate files in the output folder
            inProcess.SetStatus("Adding Front Matter");
            preProcessor.SkipChapterInformation = TocLevel;
            var frontMatter = preProcessor.InsertFrontMatter(tempFolder, false);
            inProcess.PerformStep();
            #endregion Adding Navigation and Front Matter

            #region Add Sections
            inProcess.SetStatus("Add Sections");
            var htmlFiles = new List<string>();
            var splitFiles = new List<string>();
            splitFiles.AddRange(frontMatter);
            SplittingFrontMatter(projInfo, preProcessor, defaultCss, splitFiles);
            SplittingReversal(projInfo, addRevId, langArray, defaultCss, splitFiles);
            AddBooksMoveNotes(inProcess, htmlFiles, splitFiles);
            inProcess.PerformStep();
            #endregion Add Sections

            #region Create structure and add end notes
            inProcess.SetStatus("Creating structure");
            string contentFolder = CreateContentStructure(projInfo, tempFolder);
            inProcess.PerformStep();

            // extract references file if specified
            if (References.Contains("End") && InputType.ToLower().Equals("scripture"))
            {
                inProcess.SetStatus("Creating endnote references file");
                CreateReferencesFile(contentFolder, preProcessor.ProcessedXhtml);
                splitFiles.Add(Common.PathCombine(contentFolder, ReferencesFilename));
            }
            #endregion Create structure and add end notes

            #region Font embedding
            inProcess.SetStatus("Processing fonts");
            if (!FontProcessing(projInfo, langArray, mergedCss, contentFolder))
            {
                // user cancelled the epub conversion - clean up and exit
                Environment.CurrentDirectory = curdir;
                Cursor.Current = myCursor;
                inProcess.Close();
                return false;
            }
            inProcess.PerformStep();
            #endregion Font Embedding

            #region Copy to Epub
            inProcess.SetStatus("Copy contents and styles to Epub");
            CopyStylesAndContentToEpub(mergedCss, defaultCss, htmlFiles, contentFolder);
            inProcess.PerformStep();
            #endregion Copy to Epub

            #region Insert Chapter Links
            inProcess.SetStatus("Insert Chapter Links");
            InsertChapterLinkBelowBookName(contentFolder);
            inProcess.PerformStep();
            #endregion Insert Chapter Links

            #region Process hyperlinks
#if (TIME_IT)
            DateTime dtRefStart = DateTime.Now;
#endif
            inProcess.SetStatus("Processing hyperlinks");
            if (InputType == "scripture" && References.Contains("End"))
            {
                UpdateReferenceHyperlinks(contentFolder, inProcess);
                UpdateReferenceSourcelinks(contentFolder, inProcess);
            }
            FixRelativeHyperlinks(contentFolder);

#if (TIME_IT)
            TimeSpan tsRefTotal = DateTime.Now - dtRefStart;
            Debug.WriteLine("Exportepub: time spent fixing reference hyperlinks: " + tsRefTotal);
#endif
            inProcess.PerformStep();
            #endregion Process hyperlinks

            #region Process images
            inProcess.SetStatus("Processing images");
            ProcessImages(tempFolder, contentFolder);
            inProcess.PerformStep();
            #endregion Process images

            #region Manifest and Table of Contents
            inProcess.SetStatus("Generating .epub TOC and manifest");
            CreateOpf(projInfo, contentFolder, bookId);
            CreateNcx(projInfo, contentFolder, bookId);
            inProcess.PerformStep();
            #endregion Manifest and Table of Contents

            #region Packaging
            inProcess.SetStatus("Packaging");
            if (_isUnixOs)
            {
                AddDtdInXhtml(contentFolder);
            }
            string fileName = CreateFileNameFromTitle(projInfo);
            Compress(projInfo.TempOutputFolder, Common.PathCombine(outputFolder, fileName));
            var outputPathWithFileName = Common.PathCombine(outputFolder, fileName) + ".epub";
#if (TIME_IT)
            TimeSpan tsTotal = DateTime.Now - dt1;
            Debug.WriteLine("Exportepub: time spent in .epub conversion: " + tsTotal);
#endif
            inProcess.PerformStep();
            #endregion Packaging

            #region Validate
            inProcess.SetStatus("Validate");
            ValidateAndDisplayResult(outputFolder, fileName, outputPathWithFileName);
            inProcess.PerformStep();
            #endregion Validate

            #region Clean up
            inProcess.SetStatus("Clean up");
            Common.CleanupExportFolder(outputPathWithFileName, ".tmp,.de", "_1", string.Empty);
            inProcess.PerformStep();
            #endregion Clean up

            #region Archive
            inProcess.SetStatus("Archive");
            CreateRAMP(projInfo);
            inProcess.PerformStep();
            #endregion Archive

            #region Close Reporting
            inProcess.Close();

            Environment.CurrentDirectory = curdir;
            Cursor.Current = myCursor;
            #endregion Close Reporting

            return success;
        }

        private void ValidateAndDisplayResult(string outputFolder, string fileName, string outputPathWithFileName)
        {
            // Postscript - validate the file using our epubcheck wrapper
            if (Common.Testing)
            {
                // Running the unit test - just run the validator and return the result
                var validationResults = Program.ValidateFile(outputPathWithFileName);
                Debug.WriteLine("Exportepub: validation results: " + validationResults);
            }
            else
            {
                if (MessageBox.Show(Resources.ExportCallingEpubValidator + "\r\nDo you want to Validate ePub file", Resources.ExportComplete, MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Information) == DialogResult.Yes)
                {

                    var validationDialog = new ValidationDialog { FileName = outputPathWithFileName };
                    validationDialog.ShowDialog();
                }

                DisplayOutput(outputFolder, fileName, outputPathWithFileName);
            }
        }

        private void DisplayOutput(string outputFolder, string fileName, string outputPathWithFileName)
        {
            if (File.Exists(outputPathWithFileName))
            {
                if (_isUnixOs)
                {
                    string epubFileName = fileName.Replace(" ", "") + ".epub";
                    string replaceEmptyCharacterinFileName = Common.PathCombine(outputFolder, epubFileName);
                    if (outputPathWithFileName != replaceEmptyCharacterinFileName && File.Exists(outputPathWithFileName))
                    {
                        File.Copy(outputPathWithFileName, replaceEmptyCharacterinFileName, true);
                    }

                    SubProcess.Run(outputFolder, "ebook-viewer", epubFileName, false);
                }
                else
                {
                    Process.Start(outputPathWithFileName);
                }
            }
        }

        private string CreateFileNameFromTitle(PublicationInformation projInfo)
        {
            string fileName = Title;
            if (string.IsNullOrEmpty(Title))
            {
                fileName = Path.GetFileNameWithoutExtension(projInfo.DefaultXhtmlFileWithPath);
            }
            fileName = Common.ReplaceSymbolToUnderline(fileName);
            return fileName;
        }

        private void CopyStylesAndContentToEpub(string mergedCss, string defaultCss, List<string> htmlFiles, string contentFolder)
        {
            var cssPath = Common.PathCombine(contentFolder, defaultCss);
            if (File.Exists(mergedCss))
                File.Copy(mergedCss, cssPath, true);

            var tocFiletoUpdate = string.Empty;
            SplitPageSections(htmlFiles, contentFolder, tocFiletoUpdate);
            RemoveDuplicateBookName(contentFolder);
        }

        private bool FontProcessing(PublicationInformation projInfo, string[] langArray, string mergedCss, string contentFolder)
        {
            // First, get the list of fonts used in this project
            BuildFontsList();
            // Embed fonts if needed
            if (EmbedFonts)
            {
                if (!EmbedAllFonts(langArray, contentFolder))
                {
                    return false; // user aborted
                }
            }
            // update the CSS file to reference any fonts used by the writing systems
            // (if they aren't embedded in the .epub, we'll still link to them here)

            ReferenceFonts(mergedCss, projInfo);
            return true; // successful
        }

        private string CreateContentStructure(PublicationInformation projInfo, string tempFolder)
        {
            var sb = new StringBuilder();
            sb.Append(tempFolder);
            sb.Append(Path.DirectorySeparatorChar);
            sb.Append("epub");
            string strFromOfficeFolder = Common.PathCombine(Common.GetPSApplicationPath(), "epub");
            projInfo.TempOutputFolder = sb.ToString();
            CopyFolder(strFromOfficeFolder, projInfo.TempOutputFolder);
            // set the folder where our epub content goes
            sb.Append(Path.DirectorySeparatorChar);
            sb.Append("OEBPS");
            string contentFolder = sb.ToString();
            if (!Directory.Exists(contentFolder))
            {
                Directory.CreateDirectory(contentFolder);
            }
            return contentFolder;
        }

        private void AddBooksMoveNotes(InProcess inProcess, List<string> htmlFiles, IEnumerable<string> splitFiles)
        {
            string xsltFullName = GetXsltFile();
            string getPsApplicationPath = Common.GetPSApplicationPath();
            string xsltProcessExe = Common.PathCombine(getPsApplicationPath, "XslProcess.exe");
            inProcess.SetStatus("Apply Xslt Process in html file");
            if (File.Exists(xsltProcessExe))
            {
                foreach (string file in splitFiles)
                {
                    if (_isUnixOs)
                    {
                        if (file.Contains("File2Cpy"))
                        {
                            string copyRightpage = file.Replace(".xhtml", "_.xhtml");
                            File.Copy(file, copyRightpage);
                            htmlFiles.Add(Common.PathCombine(Path.GetDirectoryName(file), (Path.GetFileNameWithoutExtension(file) + "_.xhtml")));
                            File.Delete(file);
                        }
                        else
                        {
                            Common.XsltProcess(file, xsltFullName, "_.xhtml");
                            htmlFiles.Add(Common.PathCombine(Path.GetDirectoryName(file), (Path.GetFileNameWithoutExtension(file) + "_.xhtml")));
                            File.Delete(file);
                        }
                    }
                    else
                    {
                        if (File.Exists(file))
                        {
                            const string outputExtension = "_.xhtml";
                            var args = string.Format(@"""{0}"" ""{1}"" {2} ""{3}""", file, xsltFullName,
                                                    outputExtension , getPsApplicationPath);
                            Common.RunCommand(xsltProcessExe, args, 1);
                            var inputExtension = Path.GetExtension(file);
                            Debug.Assert(inputExtension != null);
                            var xhtmlOutputFile = file.Replace(inputExtension, outputExtension);
                            // add this file to the html files list

                            if (File.Exists(xhtmlOutputFile))
                            {
                                htmlFiles.Add(xhtmlOutputFile);
                                // clean up the un-transformed file
                                File.Delete(file);
                            }
                            else
                            {
                                htmlFiles.Add(file);
                            }
                        }
                    }
                }
            }
        }

        private static Cursor UseWaitCursor()
        {
            var myCursor = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            return myCursor;
        }

        private static InProcess SetupProgressReporting(int steps)
        {
            var inProcess = new InProcess(0, steps) { Text = Resources.Exportepub_Export_Exporting__epub_file }; // create a progress bar with 7 steps (we'll add more below)
            inProcess.Show();
            inProcess.ShowStatus = true;
            return inProcess;
        }

        private void SplittingFrontMatter(PublicationInformation projInfo, PreExportProcess preProcessor, string defaultCss, List<string> splitFiles)
        {
            foreach (var file in splitFiles)
            {
                Common.SetDefaultCSS(file, defaultCss);
            }

            if ((InputType.ToLower().Equals("dictionary") && projInfo.IsLexiconSectionExist) ||
                (InputType.ToLower().Equals("scripture")))
            {
                if (projInfo.FileToProduce.ToLower() != "one")
                {
                    splitFiles.AddRange(SplitFile(preProcessor.ProcessedXhtml, projInfo));
                }
                else
                {
                    splitFiles.Add(preProcessor.ProcessedXhtml);
                }
            }

            if (_isIncludeImage == false)
            {
                foreach (string file in splitFiles)
                {
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
                    if (fileNameWithoutExtension != null && fileNameWithoutExtension.IndexOf(@"PartFile", StringComparison.CurrentCulture) == 0)
                    {
                        RemoveNodeInXhtmlFile(file);
                    }
                }
            }
        }

        private void SplittingReversal(PublicationInformation projInfo, XslCompiledTransform addRevId, string[] langArray, string defaultCss, List<string> splitFiles)
        {
            // If we are working with a dictionary and have a reversal index, process it now)
            if (projInfo.IsReversalExist)
            {
                var revFile = Common.PathCombine(Path.GetDirectoryName(projInfo.DefaultXhtmlFileWithPath), "FlexRev.xhtml");
                ReversalHacks(addRevId, langArray, defaultCss, revFile);
                // now split out the html as needed
                var fileNameWithPath = Common.SplitXhtmlFile(revFile, "letHead", "RevIndex", true);
                splitFiles.AddRange(fileNameWithPath);
            }
        }

        private void ReversalHacks(XslCompiledTransform addRevId, string[] langArray, string defaultCss, string revFile)
        {
            // EDB 10/20/2010 - TD-1629 - remove when merged CSS passes validation
            // (note that the rev file uses a "FlexRev.css", not "main.css"

            if (_isUnixOs)
            {
                Common.RemoveDTDForLinuxProcess(revFile);
            }
            Common.SetDefaultCSS(revFile, defaultCss);
            // EDB 10/29/2010 FWR-2697 - remove when fixed in FLEx
            Common.StreamReplaceInFile(revFile, "<ReversalIndexEntry_Self", "<span class='ReversalIndexEntry_Self'");
            Common.StreamReplaceInFile(revFile, "</ReversalIndexEntry_Self", "</span");
            ReversalXhtmlNamespaceHack(langArray, revFile);
            Common.ApplyXslt(revFile, addRevId);      // also removes xml:space="preserve" attributes
        }

        private static void ReversalXhtmlNamespaceHack(string[] langArray, string revFile)
        {
            if (langArray.Length > 0)
            {
                Common.StreamReplaceInFile(revFile,
                                            "<html xmlns=\"http://www.w3.org/1999/xhtml\" xml:lang=\"utf-8\" lang=\"utf-8\"",
                                            string.Format(
                                                "<html  xmlns='http://www.w3.org/1999/xhtml' xml:lang='{0}' dir='{1}'",
                                                langArray[0], Common.GetTextDirection(langArray[0])));
                Common.StreamReplaceInFile(revFile,
                                            "<html>",
                                            string.Format(
                                                "<html  xmlns='http://www.w3.org/1999/xhtml' xml:lang='{0}' dir='{1}'>",
                                                langArray[0], Common.GetTextDirection(langArray[0])));
                Common.StreamReplaceInFile(revFile, " lang=\"", " xml:lang=\"");
            }
        }

        private static void XhtmlNamespaceHack(PublicationInformation projInfo, PreExportProcess preProcessor, string[] langArray)
        {
            // EDB 10/22/2010
            // HACK: we need the preprocessed image file names (preprocessor.imageprocess()), but
            // it's missing the xml namespace that makes it a valid xhtml file. We'll add it here.
            // (The unprocessed html works fine, but doesn't have the updated links to the image files in it, 
            // so we can't use it.)
            // TODO: remove this line when TE provides valid XHTML output.

            if (langArray.Length > 0)
            {
                Common.StreamReplaceInFile(preProcessor.ProcessedXhtml, "<html>",
                                            string.Format(
                                                "<html xmlns='http://www.w3.org/1999/xhtml' xml:lang='{0}' dir='{1}'>",
                                                langArray[0], Common.GetTextDirection(langArray[0])));
                // The TE export outputs both xml:lang and lang parameters
                if (projInfo.ProjectInputType.ToLower() == "scripture")
                    Common.StreamReplaceInFile(preProcessor.ProcessedXhtml, "xml:lang=\"utf-8\" lang=\"utf-8\"", "xml:lang=\"utf-8\"");
                Common.StreamReplaceInFile(preProcessor.ProcessedXhtml, " lang=\"", " xml:lang=\"");
            }
        }

        private static string CssFullPath(PublicationInformation projInfo)
        {
            var cssFolder = Path.GetDirectoryName(projInfo.DefaultCssFileWithPath);
            var cssFullPath = Common.PathCombine(cssFolder, "epub.css");
            if (!File.Exists(cssFullPath))
            {
                cssFullPath = projInfo.DefaultCssFileWithPath;
            }
            else
            {
                string expCssLine = "@import \"" + Path.GetFileName(projInfo.DefaultCssFileWithPath) + "\";";
                Common.FileInsertText(cssFullPath, expCssLine);
            }
            return cssFullPath;
        }

        private static string NiceNameCss(PublicationInformation projInfo, string tempFolder, ref string mergedCss)
        {
            var niceNameCss = Common.PathCombine(tempFolder, "book.css");
            projInfo.DefaultCssFileWithPath = niceNameCss;
            if (niceNameCss != mergedCss)
            {
                if (File.Exists(niceNameCss))
                {
                    File.Delete(niceNameCss);
                }
                File.Copy(mergedCss, niceNameCss);
                mergedCss = niceNameCss;
            }
            return niceNameCss;
        }

        private static MergeCss _mc; // When mc is disposed it also deletes the merged file
        private static string MergeAndFilterCss(PreExportProcess preProcessor, string tempFolder, string cssFullPath)
        {
            var tempFolderName = Path.GetFileName(tempFolder);
            _mc = new MergeCss { OutputLocation = tempFolderName };
            var mergedCss = _mc.Make(cssFullPath, "book.css");
            preProcessor.RemoveDeclaration(mergedCss, "@top-");
            preProcessor.RemoveDeclaration(mergedCss, "@bottom-");
            preProcessor.RemoveDeclaration(mergedCss, "@footnote");
            preProcessor.RemoveDeclaration(mergedCss, "@page");
            preProcessor.RemoveStringInCss(mergedCss, "string-set:");
            preProcessor.RemoveStringInCss(mergedCss, "-moz-column-");
            preProcessor.RemoveStringInCss(mergedCss, "column-fill:");
            preProcessor.RemoveStringInCss(mergedCss, "-ps-outline-");
            preProcessor.RemoveStringInCss(mergedCss, "float:");
            preProcessor.RemoveStringInCss(mergedCss, "-ps-fixed-line-height:");
            preProcessor.RemoveStringInCss(mergedCss, "content: leader(");
            preProcessor.ReplaceStringInCss(mergedCss);
            preProcessor.SetDropCapInCSS(mergedCss);
            preProcessor.InsertCoverPageImageStyleInCSS(mergedCss);
            preProcessor.InsertSectionHeadID();
            return mergedCss;
        }

        private static string SetOutputFolderAndCurrentDirectory(PublicationInformation projInfo)
        {
            var outputFolder = Path.GetDirectoryName(projInfo.DefaultXhtmlFileWithPath); // finished .epub goes here
            Debug.Assert(outputFolder != null);
            Environment.CurrentDirectory = outputFolder;
            return outputFolder;
        }

        private static void XhtmlPreprocessing(PublicationInformation projInfo, PreExportProcess preProcessor)
        {
            Common.StreamReplaceInFile(preProcessor.ProcessedXhtml, "&nbsp;", Common.NonBreakingSpace);
            preProcessor.GetTempFolderPath();
            preProcessor.ImagePreprocess(false);
            preProcessor.MoveBookcodeFRTtoFront(preProcessor.ProcessedXhtml);
            if (projInfo.SwapHeadword)
            {
                preProcessor.SwapHeadWordAndReversalForm();
            }
        }

        private string[] InitializeLangArray(PublicationInformation projInfo)
        {
            _langFontDictionary = new Dictionary<string, string>();
            _embeddedFonts = new Dictionary<string, EmbeddedFont>();
            BuildLanguagesList(projInfo.DefaultXhtmlFileWithPath);
            var langArray = new string[_langFontDictionary.Keys.Count];
            _langFontDictionary.Keys.CopyTo(langArray, 0);
            return langArray;
        }

        private static XslCompiledTransform LoadFixEpubXslt()
        {
            var fixEpub = new XslCompiledTransform();
            fixEpub.Load(XmlReader.Create(Common.UsersXsl("FixEpub.xsl")));
            return fixEpub;
        }

        private static XslCompiledTransform LoadNoXmlSpaceXslt()
        {
            var noXmlSpaceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("epubConvert.noXmlSpace.xsl");
            Debug.Assert(noXmlSpaceStream != null);
            var noXmlSpace = new XslCompiledTransform();
            noXmlSpace.Load(XmlReader.Create(noXmlSpaceStream));
            return noXmlSpace;
        }

        private static XslCompiledTransform LoadAddRevIdXslt()
        {
            var addRevIdStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("epubConvert.addRevId.xsl");
            Debug.Assert(addRevIdStream != null);
            var addRevId = new XslCompiledTransform();
            addRevId.Load(XmlReader.Create(addRevIdStream));
            return addRevId;
        }

        private void CreateRAMP(PublicationInformation projInfo)
        {
            var ramp = new Ramp {ProjInputType = projInfo.ProjectInputType};
            ramp.Create(projInfo.DefaultXhtmlFileWithPath, ".epub", projInfo.ProjectInputType);
        }

        protected void SplitPageSections(IEnumerable<string> htmlFiles, string contentFolder, string tocFiletoUpdate)
        {
            foreach (string file in htmlFiles)
            {
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
                if (fileNameWithoutExtension != null)
                {
                    string name = fileNameWithoutExtension.Substring(0, 8);
                    string substring = fileNameWithoutExtension.Substring(8);
                    string dest = Common.PathCombine(contentFolder, name + substring.PadLeft(6, '0') + ".xhtml");

                    if (_isUnixOs)
                    {
                        Common.RemoveDTDForLinuxProcess(file);
                    }
                    File.Move(file, dest);
                    // split the file into smaller pieces if needed
                    var files = new List<string>();

                    if (!PageBreak && InputType.ToLower() == "dictionary")
                    {
                        files = SplitBook(dest);
                    }

                    if (InputType.ToLower() == "scripture")
                    {
                        files = SplitBook(dest);
                    }

                    if (files.Count > 1)
                    {
                        if (File.Exists(dest))
                            File.Delete(dest);
                    }

                    if (dest.Contains("File3TOC"))
                    {
                        tocFiletoUpdate = dest;
                        GetTocId(tocFiletoUpdate);
                    }

                    if (files.Count > 0 && files[0].Contains("PartFile"))
                    {
                        MapTocIdAndSectionHeadId(files);
                    }
                }
            }
            UpdateTocIdAfterFileSplit(tocFiletoUpdate);
        }

        private void UpdateTocIdAfterFileSplit(string tocFiletoUpdate)
        {
            if (tocFiletoUpdate == string.Empty || !File.Exists(tocFiletoUpdate))
            {
                return;
            }
            XmlDocument xmlDoc = Common.DeclareXMLDocument(true);
            var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
            namespaceManager.AddNamespace("xhtml", "http://www.w3.org/1999/xhtml");
            xmlDoc.Load(tocFiletoUpdate);
            XmlNodeList tagList = xmlDoc.GetElementsByTagName("a");
            if (tagList.Count > 0)
            {
                foreach (XmlNode tagValue in tagList)
                {
                    if (tagValue.Attributes != null && (tagValue.Attributes.Count > 0 && tagValue.Attributes["href"] != null))
                    {
                        if (_tocIdMapping.ContainsKey(tagValue.Attributes["href"].Value))
                            tagValue.Attributes["href"].Value = _tocIdMapping[tagValue.Attributes["href"].Value];
                    }
                }
            }
            xmlDoc.Save(tocFiletoUpdate);
        }

        private void MapTocIdAndSectionHeadId(List<string> files)
        {
            var hasBookmark = false;
            if (files.Count > 0)
            {
                var firstName = Path.GetFileName(files[0]);
                Debug.Assert(!string.IsNullOrEmpty(firstName));
                foreach (string idVal in _tocIDs)
                {
                    var name = Path.GetFileNameWithoutExtension(idVal);
                    Debug.Assert(!string.IsNullOrEmpty(name));
                    if (!idVal.Contains("#"))
                    {
                        if (firstName.Contains(name))
                            _tocIdMapping.Add(idVal, firstName);
                    }
                    else
                    {
                        hasBookmark = true;
                    }
                }
            }
            if (!hasBookmark) return;
            foreach (string partFile in files)
            {
                XmlDocument xDoc = Common.DeclareXMLDocument(true);
                var namespaceManager = new XmlNamespaceManager(xDoc.NameTable);
                namespaceManager.AddNamespace("xhtml", "http://www.w3.org/1999/xhtml");
                xDoc.Load(partFile);
                XmlNodeList divList = xDoc.GetElementsByTagName("div");
                if (divList.Count > 0)
                {
                    foreach (XmlNode divTag in divList)
                    {
                        if (divTag.Attributes != null && divTag.Attributes.Count > 0 && divTag.Attributes["class"] != null &&
                            divTag.Attributes["id"] != null
                            && divTag.Attributes["class"].Value == "Section_Head")
                        {
                            string sectionHeadId = divTag.Attributes["id"].Value;
                            foreach (string idVal in _tocIDs)
                            {
                                if (idVal.IndexOf('#') > 0)
                                {
                                    var val = idVal.Split('#');
                                    var oldPartFileName = Common.LeftString(val[0], "_");
                                    var newPartFileName =
                                        Common.LeftString(Path.GetFileNameWithoutExtension(partFile), "_");
                                    if (oldPartFileName == newPartFileName && val[1] == sectionHeadId)
                                    {
                                        if (!_tocIdMapping.ContainsKey(idVal))
                                            _tocIdMapping.Add(idVal,
                                                              Path.GetFileName(partFile) + "#" + sectionHeadId);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void GetTocId(string tocFiletoUpdate)
        {
            XmlDocument xDoc = Common.DeclareXMLDocument(true);
            var namespaceManager = new XmlNamespaceManager(xDoc.NameTable);
            namespaceManager.AddNamespace("xhtml", "http://www.w3.org/1999/xhtml");
            xDoc.Load(tocFiletoUpdate);
            XmlNodeList anchorList = xDoc.GetElementsByTagName("a");
            if (anchorList.Count > 0)
            {
                foreach (XmlNode variable1 in anchorList)
                {
                    if (variable1.Attributes != null && variable1.Attributes.Count > 0 && variable1.Attributes["href"] != null)
                    {
                        _tocIDs.Add(variable1.Attributes["href"].Value);
                    }
                }
            }
        }

        private bool GetIncludeImageStatus(string cssFileName)
        {
            try
            {
                if (cssFileName.Trim().Length == 0) { return true; }
                Param.LoadSettings();
                var xDoc = Common.DeclareXMLDocument(false);
                string path = Param.SettingOutputPath;
                xDoc.Load(path);
                var xPath = "//stylePick/styles/others/style[@file='" + cssFileName + ".css']/styleProperty[@name='IncludeImage']/@value";
                var includeImageNode = xDoc.SelectSingleNode(xPath);
                if (includeImageNode != null && includeImageNode.InnerText == "No")
                    _isIncludeImage = false;
            }
            catch { }
            return _isIncludeImage;
        }

        private void RemoveNodeInXhtmlFile(string fileName)
        {
            //Removed NoteTargetReference tag from XHTML file
            XmlDocument xDoc = Common.DeclareXMLDocument(false);
            var namespaceManager = new XmlNamespaceManager(xDoc.NameTable);
            namespaceManager.AddNamespace("xhtml", "http://www.w3.org/1999/xhtml");
            xDoc.Load(fileName);
            XmlElement elmRoot = xDoc.DocumentElement;
            //If includeImage is false, removes the img -> parent tag
            if (_isIncludeImage == false)
            {
                string[] pictureClass = { "pictureCaption", "pictureColumn", "picturePage" };
                foreach (string clsName in pictureClass)
                {
                    string xPath = "//xhtml:div[@class='" + clsName + "']";
                    if (elmRoot != null)
                    {
                        XmlNodeList pictCaptionNode = elmRoot.SelectNodes(xPath, namespaceManager);
                        if (pictCaptionNode != null && pictCaptionNode.Count > 0)
                        {
                            for (int i = 0; i < pictCaptionNode.Count; i++)
                            {
                                var parentNode = pictCaptionNode[i].ParentNode;
                                if (parentNode != null)
                                    parentNode.RemoveChild(pictCaptionNode[i]);
                            }
                        }
                    }
                }

                if (elmRoot != null && _isIncludeImage == false)
                {
                    XmlNodeList imgNodes = elmRoot.GetElementsByTagName("img");
                    if (imgNodes.Count > 0)
                    {
                        int imgCount = imgNodes.Count;
                        for (int i = 0; i < imgCount; i++)
                        {
                            var parentNode = imgNodes[0].ParentNode;
                            if (parentNode != null)
                                parentNode.RemoveChild(imgNodes[0]);
                        }
                    }
                }
            }
            xDoc.Save(fileName);
        }

        #region Private Functions
        #region Handle After Before
        /// <summary>
        /// Inserting After & Before content to XHTML file
        /// </summary>
        private void InsertBeforeAfterInXhtml(PublicationInformation projInfo)
        {
            if (projInfo == null) return;
            if (projInfo.DefaultXhtmlFileWithPath == null || projInfo.DefaultCssFileWithPath == null) return;
            if (projInfo.DefaultXhtmlFileWithPath.Trim().Length == 0 || projInfo.DefaultCssFileWithPath.Trim().Length == 0) return;

            var cssTree = new CssTree();
            Dictionary<string, Dictionary<string, string>> cssClass = cssTree.CreateCssProperty(projInfo.DefaultCssFileWithPath, true);

            var afterBeforeProcess = new AfterBeforeProcessEpub();
            afterBeforeProcess.RemoveAfterBefore(projInfo, cssClass, cssTree.SpecificityClass, cssTree.CssClassOrder);
            PseudoClass = afterBeforeProcess._psuedoClassName;

            if (projInfo.IsReversalExist && projInfo.ProjectInputType.ToLower() == "dictionary")
            {
                cssClass = cssTree.CreateCssProperty(projInfo.DefaultRevCssFileWithPath, true);
                string originalDefaultXhtmlFileName = projInfo.DefaultXhtmlFileWithPath;
                projInfo.DefaultXhtmlFileWithPath = Common.PathCombine(Path.GetDirectoryName(projInfo.DefaultXhtmlFileWithPath), "FlexRev.xhtml");
                var afterBeforeProcessReversal = new AfterBeforeProcessEpub();
                afterBeforeProcessReversal.RemoveAfterBefore(projInfo, cssClass, cssTree.SpecificityClass, cssTree.CssClassOrder);
                Common.StreamReplaceInFile(projInfo.DefaultXhtmlFileWithPath, "&nbsp;", Common.NonBreakingSpace);
                projInfo.DefaultXhtmlFileWithPath = originalDefaultXhtmlFileName;
            }
        }
        #endregion

        #region Property persistence
        /// <summary>
        /// Loads the settings file and pulls out the values we look at.
        /// </summary>
        private void LoadPropertiesFromSettings()
        {
            // Load User Interface Collection Parameters
            Param.LoadSettings();
            string organization;
            try
            {
                // get the organization
                organization = Param.Value["Organization"];
            }
            catch (Exception)
            {
                // shouldn't happen (ExportThroughPathway dialog forces the user to select an organization), 
                // but just in case, specify a default org.
                organization = "SIL International";
            }
            string layout = Param.GetItem("//settings/property[@name='LayoutSelected']/@value").Value;
            Dictionary<string, string> othersfeature = Param.GetItemsAsDictionary("//stylePick/styles/others/style[@name='" + layout + "']/styleProperty");
            // Title (book title in Configuration Tool UI / dc:title in metadata)
            Title = Param.GetMetadataValue(Param.Title, organization) ?? ""; // empty string if null / not found
            // Creator (dc:creator))
            Creator = Param.GetMetadataValue(Param.Creator, organization) ?? ""; // empty string if null / not found
            // information
            Description = Param.GetMetadataValue(Param.Description, organization) ?? ""; // empty string if null / not found
            // Source
            Source = Param.GetMetadataValue(Param.Source, organization) ?? ""; // empty string if null / not found
            // Format
            Format = Param.GetMetadataValue(Param.Format, organization) ?? ""; // empty string if null / not found
            // Publisher
            Publisher = Param.GetMetadataValue(Param.Publisher, organization) ?? ""; // empty string if null / not found
            // Coverage
            Coverage = Param.GetMetadataValue(Param.Coverage, organization) ?? ""; // empty string if null / not found
            // Rights (dc:rights)
            Rights = Param.GetMetadataValue(Param.CopyrightHolder, organization) ?? ""; // empty string if null / not found
            Rights = Common.UpdateCopyrightYear(Rights);
            EmbedFonts = !othersfeature.ContainsKey("EmbedFonts") || (othersfeature["EmbedFonts"].Trim().Equals("Yes"));
            IncludeFontVariants = !othersfeature.ContainsKey("IncludeFontVariants") || (othersfeature["IncludeFontVariants"].Trim().Equals("Yes"));
            if (othersfeature.ContainsKey("MaxImageWidth"))
            {
                try
                {
                    MaxImageWidth = int.Parse(othersfeature["MaxImageWidth"].Trim());
                }
                catch (Exception)
                {
                    MaxImageWidth = 600;
                }
            }
            else
            {
                MaxImageWidth = 600;
            }
            TocLevel = othersfeature.ContainsKey("TOCLevel") ? othersfeature["TOCLevel"].Trim() : "";
            DefaultFont = othersfeature.ContainsKey("DefaultFont") ? othersfeature["DefaultFont"].Trim() : "Charis SIL";
            DefaultAlignment = othersfeature.ContainsKey("DefaultAlignment") ? othersfeature["DefaultAlignment"].Trim() : "Justified";
            ChapterNumbers = othersfeature.ContainsKey("ChapterNumbers") ? othersfeature["ChapterNumbers"].Trim() : "Drop Cap";
            References = othersfeature.ContainsKey("References") ? othersfeature["References"].Trim() : "After Each Section";

            // base font size
            if (othersfeature.ContainsKey("BaseFontSize"))
            {
                try
                {
                    BaseFontSize = int.Parse(othersfeature["BaseFontSize"].Trim());
                }
                catch (Exception)
                {
                    BaseFontSize = 13;
                }
            }
            else
            {
                BaseFontSize = 13;
            }
            // default line height
            if (othersfeature.ContainsKey("DefaultLineHeight"))
            {
                try
                {
                    DefaultLineHeight = int.Parse(othersfeature["DefaultLineHeight"].Trim());
                }
                catch (Exception)
                {
                    DefaultLineHeight = 125;
                }
            }
            else
            {
                DefaultLineHeight = 125;
            }
            // Missing Font
            // Note that the Embed Font enum value doesn't apply here (if it were to appear, we'd fall to the Default
            // "Prompt user" case
            if (othersfeature.ContainsKey("MissingFont"))
            {
                switch (othersfeature["MissingFont"].Trim())
                {
                    case "Use Fallback Font":
                        MissingFont = FontHandling.SubstituteDefaultFont;
                        break;
                    case "Cancel Export":
                        MissingFont = FontHandling.CancelExport;
                        break;
                    default: // "Prompt User" case goes here
                        MissingFont = FontHandling.PromptUser;
                        break;
                }
            }
            else
            {
                MissingFont = FontHandling.PromptUser;
            }
            // Non SIL Font
            if (othersfeature.ContainsKey("NonSILFont"))
            {
                switch (othersfeature["NonSILFont"].Trim())
                {
                    case "Embed Font Anyway":
                        NonSilFont = FontHandling.EmbedFont;
                        break;
                    case "Use Fallback Font":
                        NonSilFont = FontHandling.SubstituteDefaultFont;
                        break;
                    case "Cancel Export":
                        NonSilFont = FontHandling.CancelExport;
                        break;
                    default: // "Prompt User" case goes here
                        NonSilFont = FontHandling.PromptUser;
                        break;
                }
            }
            else
            {
                NonSilFont = FontHandling.PromptUser;
            }
        }
        #endregion

        #region xslt processing
        /// <summary>
        /// Helper method that copies the epub xslt file into the temp directory, optionally inserts some
        /// processing commands, then returns the path to the modified file.
        /// </summary>
        /// <returns>Full path / filename of the xslt file</returns>
        private string GetXsltFile()
        {
            string xsltFullName = Common.FromRegistry("TE_XHTML-to-epub_XHTML.xslt");
            if (!File.Exists(xsltFullName))
                return "";
            var tempXslt = Common.PathCombine(Path.GetTempPath(), Path.GetFileName(xsltFullName));
            File.Copy(xsltFullName, tempXslt, true);
            xsltFullName = tempXslt;

            // Modify the local XSLT for the following conditions:
            // - Scriptures with a inline footnotes (References == "After Each Section"):
            //   adds the 

            if (InputType.ToLower().Equals("scripture") && References.Contains("Section"))
            {
                // add references inline, after each section (first change)
                const string searchText = "<!-- Section div reference processing -->";
                var sbRef = new StringBuilder();
                sbRef.AppendLine(searchText);
                sbRef.AppendLine("<xsl:if test=\"@class = 'scrSection'\">");
                sbRef.Append("<xsl:if test=\"(count(descendant::xhtml:span[@class='Note_General_Paragraph']) +");
                sbRef.AppendLine(" count(descendant::xhtml:span[@class='Note_CrossHYPHENReference_Paragraph'])) > 0\">");
                sbRef.AppendLine("<xsl:element name=\"ul\">");
                sbRef.AppendLine("<xsl:attribute name=\"class\"><xsl:text>footnotes</xsl:text></xsl:attribute>");
                sbRef.AppendLine("<!-- general notes - use the note title for the list bullet -->");
                sbRef.AppendLine("<xsl:for-each select=\"descendant::xhtml:span[@class='Note_General_Paragraph']\">");
                sbRef.AppendLine("<xsl:element name=\"li\">");
                sbRef.AppendLine("<xsl:attribute name=\"id\"><xsl:text>FN_</xsl:text><xsl:value-of select=\"@id\"/></xsl:attribute>");
                sbRef.AppendLine("<xsl:element name=\"a\">");
                sbRef.AppendLine("<xsl:attribute name=\"href\"><xsl:text>#</xsl:text><xsl:value-of select=\"@id\"/></xsl:attribute>");
                sbRef.AppendLine("<xsl:text>[</xsl:text><xsl:value-of select=\"@title\"/><xsl:text>]</xsl:text>");
                sbRef.AppendLine("</xsl:element><xsl:text> </xsl:text><xsl:value-of select=\".\"/></xsl:element>");
                sbRef.AppendLine("</xsl:for-each>");
                sbRef.AppendLine("<!-- cross-references - use) the verse number for the list bullet -->");
                sbRef.AppendLine("<xsl:for-each select=\"descendant::xhtml:span[@class='Note_CrossHYPHENReference_Paragraph']\">");
                sbRef.AppendLine("<xsl:element name=\"li\">");
                sbRef.AppendLine("<xsl:attribute name=\"id\"><xsl:text>FN_</xsl:text><xsl:value-of select=\"@id\"/></xsl:attribute>");
                sbRef.AppendLine("<xsl:element name=\"a\">");
                sbRef.AppendLine("<xsl:attribute name=\"href\"><xsl:text>#</xsl:text><xsl:value-of select=\"@id\"/></xsl:attribute>");
                if (_isNoteTargetReferenceExists)
                {
                    sbRef.AppendLine("<xsl:value-of select=\"xhtml:span[@class='Note_Target_Reference']\"/>");
                    sbRef.AppendLine("</xsl:element><xsl:text> </xsl:text>");
                    sbRef.AppendLine("<xsl:for-each select=\"xhtml:span[not(@class ='Note_Target_Reference')]\"><xsl:value-of select=\".\"/></xsl:for-each>");
                    sbRef.AppendLine("</xsl:element></xsl:for-each>");
                }
                else
                {
                    sbRef.AppendLine("<xsl:value-of select=\"preceding::xhtml:span[@class='Chapter_Number'][1]\"/><xsl:text>:</xsl:text>");
                    sbRef.AppendLine("<xsl:value-of select=\"preceding::xhtml:span[@class='Verse_Number'][1]\"/>");
                    sbRef.AppendLine("</xsl:element><xsl:text> </xsl:text><xsl:value-of select=\".\"/></xsl:element></xsl:for-each>");
                }

                sbRef.AppendLine("</xsl:element></xsl:if></xsl:if>");
                Common.StreamReplaceInFile(xsltFullName, searchText, sbRef.ToString());
                // add references inline (second change)
                sbRef.Length = 0;
                const string searchText2 = "<!-- secondary Section div reference processing -->";
                sbRef.AppendLine(searchText2);
                sbRef.Append("<xsl:if test=\"(count(descendant::xhtml:span[@class='Note_General_Paragraph']) + ");
                sbRef.AppendLine("count(descendant::xhtml:span[@class='Note_CrossHYPHENReference_Paragraph'])) > 0\">");
                sbRef.AppendLine("<xsl:element name=\"ul\">");
                sbRef.AppendLine("<xsl:attribute name=\"class\"><xsl:text>footnotes</xsl:text></xsl:attribute>");
                sbRef.AppendLine("<!-- general) notes - use the note title for the list bullet -->");
                sbRef.AppendLine("<xsl:for-each select=\"descendant::xhtml:span[@class='Note_General_Paragraph']\">");
                sbRef.AppendLine("<xsl:element name=\"li\">");
                sbRef.AppendLine("<xsl:attribute name=\"id\"><xsl:text>FN_</xsl:text><xsl:value-of select=\"@id\"/></xsl:attribute>");
                sbRef.AppendLine("<xsl:element name=\"a\">");
                sbRef.AppendLine("<xsl:attribute name=\"href\"><xsl:text>#</xsl:text><xsl:value-of select=\"@id\"/></xsl:attribute>");
                sbRef.AppendLine("<xsl:text>[</xsl:text><xsl:value-of select=\"@title\"/><xsl:text>]</xsl:text>");
                sbRef.AppendLine("</xsl:element><xsl:text> </xsl:text><xsl:value-of select=\".\"/></xsl:element></xsl:for-each>");
                sbRef.AppendLine("<!-- cross-references - use the verse number for the list bullet -->");
                sbRef.AppendLine("<xsl:for-each select=\"descendant::xhtml:span[@class='Note_CrossHYPHENReference_Paragraph']\">");
                sbRef.AppendLine("<xsl:element name=\"li\">");
                sbRef.AppendLine("<xsl:attribute name=\"id\"><xsl:text>FN_</xsl:text><xsl:value-of select=\"@id\"/></xsl:attribute>");
                sbRef.AppendLine("<xsl:element name=\"a\">");
                sbRef.AppendLine("<xsl:attribute name=\"href\"><xsl:text>#</xsl:text><xsl:value-of select=\"@id\"/></xsl:attribute>");
                if (_isNoteTargetReferenceExists)
                {
                    sbRef.AppendLine("<xsl:value-of select=\"xhtml:span[@class='Note_Target_Reference']\"/>");
                    sbRef.AppendLine("</xsl:element><xsl:text> </xsl:text>");
                    sbRef.AppendLine("<xsl:for-each select=\"xhtml:span[not(@class ='Note_Target_Reference')]\"><xsl:value-of select=\".\"/></xsl:for-each>");
                    sbRef.AppendLine("</xsl:element>");
                }
                else
                {
                    sbRef.AppendLine("<xsl:value-of select=\"preceding::xhtml:span[@class='Chapter_Number'][1]\"/><xsl:text>:</xsl:text>");
                    sbRef.AppendLine("<xsl:value-of select=\"preceding::xhtml:span[@class='Verse_Number'][1]\"/>");
                    sbRef.AppendLine("</xsl:element><xsl:text> </xsl:text><xsl:value-of select=\".\"/></xsl:element>");
                }

                sbRef.AppendLine("</xsl:for-each></xsl:element></xsl:if>");
                Common.StreamReplaceInFile(xsltFullName, searchText2, sbRef.ToString());
            }
            return xsltFullName;
        }
        #endregion

        #region CSS processing
        /// <summary>
        /// Modifies the CSS based on the parameters from the Configuration Tool:
        /// - BaseFontSize
        /// - DefaultLineHeight
        /// - DefaultAlignment
        /// - ChapterNumbers
        /// </summary>
        /// <param name="cssFile"></param>
        private void CustomizeCss(string cssFile)
        {
            if (!File.Exists(cssFile)) return;
            // BaseFontSize and DefaultLineHeight - body element only
            var sb = new StringBuilder();
            sb.AppendLine("body {");
            sb.Append("font-size: ");
            sb.Append(BaseFontSize);
            sb.AppendLine("pt;");
            sb.Append("line-height: ");
            sb.Append(DefaultLineHeight);
            sb.AppendLine("%;");
            Common.StreamReplaceInFile(cssFile, "body {", sb.ToString());
            // ChapterNumbers - scripture only
            if (InputType == "scripture")
            {
                // ChapterNumbers (drop cap or in margin) - .Chapter_Number and .Paragraph1 class elements
                sb.Length = 0;  // reset the stringbuilder
                sb.AppendLine(".Chapter_Number {");
                sb.Append("font-size: ");
                if (ChapterNumbers == "Drop Cap")
                {
                    sb.AppendLine("250%;");
                    // vertical alignment of Cap specified by setting the padding-top to (defaultlineheight / 2)
                    sb.Append("padding-top: ");
                    sb.Append(BaseFontSize / 2);
                    sb.AppendLine("pt;");
                }
                else
                {
                    sb.AppendLine("24pt;");
                }
                Common.StreamReplaceInFile(cssFile, ".Chapter_Number {", sb.ToString());
            }
            // DefaultAlignment - several spots in the css file
            sb.Length = 0; // reset the stringbuilder
            sb.Append("text-align: ");
            sb.Append(DefaultAlignment.ToLower());
            sb.AppendLine(";");
            Common.StreamReplaceInFile(cssFile, "text-align:left;", sb.ToString());
        }
        #endregion

        #region Font Handling
        /// <summary>
        /// Handles font embedding for the .epub file. The fonts are verified before they are copied over, to
        /// make sure they (1) exist on the system and (2) are SIL produced. For the latter, the user is able
        /// to embed them anyway if they click that they have the appropriate rights (it's an honor system approach).
        /// </summary>
        /// <param name="langArray"></param>
        /// <param name="contentFolder"></param>
        /// <returns></returns>
        private bool EmbedAllFonts(string[] langArray, string contentFolder)
        {
            var nonSilFonts = new Dictionary<EmbeddedFont, string>();
            // Build the list of non-SIL fonts in use
            foreach (var embeddedFont in _embeddedFonts)
            {
                if (!embeddedFont.Value.CanRedistribute)
                {
                    foreach (var language in _langFontDictionary.Keys)
                    {
                        if (_langFontDictionary[language].Equals(embeddedFont.Key))
                        {
                            // add this language to the list of langs that use this font
                            string langs;
                            if (nonSilFonts.TryGetValue(embeddedFont.Value, out langs))
                            {
                                // existing entry - add this language to the list of langs that use this font
                                var sbName = new StringBuilder();
                                sbName.Append(langs);
                                sbName.Append(", ");
                                sbName.Append(language);
                                // set the value
                                nonSilFonts[embeddedFont.Value] = sbName.ToString();
                            }
                            else
                            {
                                // new entry
                                nonSilFonts.Add(embeddedFont.Value, language);
                            }
                        }
                    }
                }
            }
            // If there are any non-SIL fonts in use, show the Font Warning Dialog
            // (possibly multiple times) and replace our embedded font items if needed
            // (if we're running a test, skip the dialog and just embed the font)
            if (nonSilFonts.Count > 0 && !Common.Testing)
            {
                var dlg = new FontWarningDlg {RepeatAction = false, RemainingIssues = nonSilFonts.Count - 1};
                // Handle the cases where the user wants to automatically process non-SIL / missing fonts
                if (NonSilFont == FontHandling.CancelExport)
                {
                    // TODO: implement message box
                    // Give the user a message indicating there's a non-SIL font in their writing system, and
                    // to go fix the problem. Don't let them continue with the export.
                    return false;
                }
                if (NonSilFont != FontHandling.PromptUser)
                {
                    dlg.RepeatAction = true; // the handling picks up below...
                    dlg.SelectedFont = DefaultFont;
                }
                foreach (var nonSilFont in nonSilFonts)
                {
                    dlg.MyEmbeddedFont = nonSilFont.Key.Name;
                    dlg.Languages = nonSilFont.Value;
                    bool isMissing = (nonSilFont.Key.Filename == null);
                    bool isManualProcess = ((isMissing == false && NonSilFont == FontHandling.PromptUser) || (isMissing == true && MissingFont == FontHandling.PromptUser));
                    if (dlg.RepeatAction)
                    {
                        // user wants to repeat the last action - if the last action
                        // was to change the font, change this one as well
                        // (this is also where the automatic FontHandling takes place)
                        if ((!dlg.UseFontAnyway() && !nonSilFont.Key.Name.Equals(dlg.SelectedFont) && isManualProcess) || // manual "repeat this action" for non-SIL AND missing fonts
                            (isMissing == false && NonSilFont == FontHandling.SubstituteDefaultFont && !nonSilFont.Key.Name.Equals(DefaultFont)) || // automatic for non-SIL fonts
                            (isMissing == true && MissingFont == FontHandling.SubstituteDefaultFont && !nonSilFont.Key.Name.Equals(DefaultFont))) // automatic for missing fonts
                        {
                            // the user has chosen a different (SIL) font - 
                            // create a new EmbeddedFont and add it to the list
                            _embeddedFonts.Remove(nonSilFont.Key.Name);
                            var newFont = new EmbeddedFont(dlg.SelectedFont);
                            _embeddedFonts[dlg.SelectedFont] = newFont; // set index value adds if it doesn't exist
                            // also update the references in _langFontDictionary
                            foreach (var lang in langArray)
                            {
                                if (_langFontDictionary[lang] == nonSilFont.Key.Name)
                                {
                                    _langFontDictionary[lang] = dlg.SelectedFont;
                                }
                            }
                        }
                        // the UseFontAnyway checkbox (and FontHandling.EmbedFont) cases fall through here -
                        // The current non-SIL font is ignored and embedded below
                        continue;
                    }
                    // sanity check - are there any SIL fonts installed?
                    int count = dlg.BuildSILFontList();
                    if (count == 0)
                    {
                        // No SIL fonts found (returns a DialogResult.Abort):
                        // tell the user there are no SIL fonts installed, and allow them to Cancel
                        // and install the fonts now
                        if (MessageBox.Show(Resources.NoSILFontsMessage, Resources.NoSILFontsTitle,
                                             MessageBoxButtons.OKCancel, MessageBoxIcon.Warning)
                            == DialogResult.Cancel)
                        {
                            // user cancelled the operation - Cancel out of the whole .epub export
                            return false;
                        }
                        // user clicked OK - leave the embedded font list alone and continue the export
                        // (presumably the user has the proper rights to this font, even though it isn't
                        // an SIL font)
                        break;
                    }
                    // show the dialog
                    DialogResult result = dlg.ShowDialog();
                    if (result == DialogResult.OK)
                    {
                        if (!dlg.UseFontAnyway() && !nonSilFont.Key.Name.Equals(dlg.SelectedFont))
                        {
                            // the user has chosen a different (SIL) font - 
                            // create a new EmbeddedFont and add it to the list
                            _embeddedFonts.Remove(nonSilFont.Key.Name);
                            var newFont = new EmbeddedFont(dlg.SelectedFont);
                            _embeddedFonts[dlg.SelectedFont] = newFont; // set index value adds if it doesn't exist
                            // also update the references in _langFontDictionary
                            foreach (var lang in langArray)
                            {
                                if (_langFontDictionary[lang] == nonSilFont.Key.Name)
                                {
                                    _langFontDictionary[lang] = dlg.SelectedFont;
                                }
                            }
                        }
                    }
                    else if (result == DialogResult.Cancel)
                    {
                        // User cancelled - Cancel out of the whole .epub export
                        return false;
                    }
                    // decrement the remaining issues for the next dialog display
                    dlg.RemainingIssues--;
                }
            }
            // copy all the fonts over
            foreach (var embeddedFont in _embeddedFonts.Values)
            {
                if (embeddedFont.Filename == null)
                {
                    Debug.WriteLine("ERROR: embedded font " + embeddedFont.Name + " is not installed - skipping");
                    continue;
                }
                string dest = Common.PathCombine(contentFolder, Path.GetFileName(embeddedFont.Filename));
                if (embeddedFont.Filename != string.Empty && File.Exists(embeddedFont.Filename))
                {
                    File.Copy(embeddedFont.Filename, dest, true);

                    if (IncludeFontVariants)
                    {
                        // italic
                        if (embeddedFont.HasItalic && embeddedFont.ItalicFilename.Trim().Length > 0 &&
                            embeddedFont.ItalicFilename != embeddedFont.Filename)
                        {
                            dest = Common.PathCombine(contentFolder, Path.GetFileName(embeddedFont.ItalicFilename));
                            if (!File.Exists(dest))
                            {
                                File.Copy(embeddedFont.ItalicFilename, dest, true);
                            }
                        }
                        // bold
                        if (embeddedFont.HasBold && embeddedFont.BoldFilename.Trim().Length > 0 &&
                            embeddedFont.BoldFilename != embeddedFont.Filename)
                        {
                            dest = Common.PathCombine(contentFolder, Path.GetFileName(embeddedFont.BoldFilename));
                            if (!File.Exists(dest))
                            {
                                File.Copy(embeddedFont.BoldFilename,
                                          dest, true);
                            }
                        }
                    }
                }
            }
            // clean up
            if (nonSilFonts.Count > 0)
            {
                nonSilFonts.Clear();
            }
            return true;
        }

        private string IncludeQuoteOnFontName(string fontname)
        {
            if (fontname.Trim().IndexOf(' ') > 0)
            {
                fontname = "'" + fontname + "'";
            }
            return fontname;
        }

        /// <summary>
        /// Inserts links in the CSS file to the fonts used by the writing systems:
        /// - If the fonts are embedded, adds a @font-face declaration referencing the .ttf file 
        ///   that's found in the archive
        /// - Sets the font-family for the body:lang selector to the referenced font
        /// </summary>
        /// <param name="cssFile"></param>
        /// <param name="projInfo">Project information - used to find path to reversal file.</param>
        private void ReferenceFonts(string cssFile, IPublicationInformation projInfo)
        {
            if (!File.Exists(cssFile)) return;
            // read in the CSS file
            string mainTextDirection = "ltr";
            var reader = new StreamReader(cssFile);
            string content = reader.ReadToEnd();
            reader.Close();
            var sb = new StringBuilder();
            // write a timestamp for field troubleshooting
            WriteProductNameAndTimeStamp(sb);
            // If we're embedding the fonts, build the @font-face elements))))
            if (EmbedFonts)
            {
                foreach (var embeddedFont in _embeddedFonts.Values)
                {
                    if (embeddedFont.Filename == null)
                    {
                        WriteMissingFontMessage(sb, embeddedFont);
                        continue;
                    }
                    WriteFontDeclarationBlock(sb, embeddedFont);
                    // if we're also embedding the font variants (bold, italic), reference them now
                    if (IncludeFontVariants)
                    {
                        // Italic version
                        if (embeddedFont.HasItalic)
                        {
                            WriteItalicVariantDeclarationBlock(sb, embeddedFont);
                        }
                        // Bold version
                        if (embeddedFont.HasBold)
                        {
                            WriteBoldVariantDeclarationBlock(sb, embeddedFont);
                        }
                    }
                }
            }
            // add :lang pseudo-elements for each language and set them to the proper font
            bool firstLang = true;
            foreach (var language in _langFontDictionary)
            {
                var languageKey = language.Key;
                var languageName = language.Value;
                EmbeddedFont embeddedFont;
                // If this is the first language in the loop (i.e., the main language),
                // set the font for the body element
                if (firstLang)
                {
                    mainTextDirection = Common.GetTextDirection(languageKey);
                    embeddedFont = WriteMainLanguageDeclarationBlock(mainTextDirection, sb, languageName);
                    if (IncludeFontVariants)
                    {
                        // Italic version
                        if (embeddedFont != null)
                            if (embeddedFont.HasItalic)
                            {
                                embeddedFont = WriteItalicLanguageFondDeclarationBlock(sb, embeddedFont, languageName);
                            }
                        // Bold version
                        if (embeddedFont != null)
                            if (embeddedFont.HasBold)
                            {
                                embeddedFont = WriteBoldLanguageFontDeclarationBlock(sb, embeddedFont, languageName);
                            }
                    }

                    var revFile = Common.PathCombine(Path.GetDirectoryName(projInfo.DefaultXhtmlFileWithPath), "FlexRev.xhtml");

                    if (File.Exists(revFile))
                    {
                        string reverseSenseNumberFont = GetLanguageForReversalNumber(revFile, languageKey);
                        WriteReversalFontDeclaration(sb, languageName, languageKey, reverseSenseNumberFont);
                    }

                    // finished processing - clear the flag
                    firstLang = false;
                }

                // set the font for the *:lang(xxx) pseudo-element
                embeddedFont = WriteGenericLanguageDeclarationBlock(sb, languageKey, languageName);

                if (IncludeFontVariants)
                {
                    // italic version
                    if (embeddedFont != null)
                        if (embeddedFont.HasItalic)
                        {
                            embeddedFont = WriteItalicClassesDeclarationBlock(sb, languageKey, languageName, embeddedFont);
                        }
                    // bold version
                    if (embeddedFont != null)
                        if (embeddedFont.HasBold)
                        {
                            embeddedFont = WriteBoldClassesDeclarationBlock(sb, languageKey, languageName, embeddedFont);
                        }
                }
            }

            sb.AppendLine("/* end auto-generated font info */");
            sb.AppendLine();
            RemovesImportStatementIfItExists(content, sb);
            WriteUpdatedCssFile(cssFile, sb);
            AddDirectionAndPaddingForScripture(cssFile, mainTextDirection, sb);
        }

        private static void WriteUpdatedCssFile(string cssFile, StringBuilder sb)
        {
            var writer = new StreamWriter(cssFile);
            writer.Write(sb.ToString());
            writer.Close();
        }

        private static void RemovesImportStatementIfItExists(string content, StringBuilder sb)
        {
            // nuke the @import statement (we're going off one CSS file here)
            //string contentNoImport = content.Substring(content.IndexOf(';') + 1);
            //sb.Append(contentNoImport);
            // remove the @import statement IF it exists in the css file
            sb.Append(content.StartsWith("@import") ? content.Substring(content.IndexOf(';') + 1) : content);
        }

        private void AddDirectionAndPaddingForScripture(string cssFile, string mainTextDirection, StringBuilder sb)
        {
            // Now that we know the text direction, we can add some padding info for the chapter numbers
            // (Scripture only)
            if (InputType == "scripture")
            {
                var mainDirection = mainTextDirection.ToLower().Equals("ltr") ? "left" : "right";
                sb.Length = 0; // reset the stringbuilder
                sb.AppendLine(".Chapter_Number {");
                sb.Append("float: ");
                sb.Append(mainDirection);
                sb.AppendLine(";");
                sb.Append("padding-right: 5pt; padding-");
                sb.Append(mainDirection);
                sb.Append(": ");
                sb.Append((ChapterNumbers == "Drop Cap") ? "4%;" : "5pt;");
                Common.StreamReplaceInFile(cssFile, ".Chapter_Number {", sb.ToString());
            }
        }

        private EmbeddedFont WriteBoldClassesDeclarationBlock(StringBuilder sb, string languageKey, string languageName, EmbeddedFont embeddedFont)
        {
            // dictionary
            sb.Append(".headword:lang(");
            sb.Append(languageKey);
            sb.Append("), .headword-minor:lang(");
            sb.Append(languageKey);
            sb.Append("), .LexSense-publishStemMinorPrimaryTarget-OwnerOutlinePub:lang(");
            sb.Append(languageKey);
            sb.Append("), .LexEntry-publishStemMinorPrimaryTarget-MLHeadWordPub:lang(");
            sb.Append(languageKey);
            sb.Append("), .xsensenumber:lang(");
            sb.Append(languageKey);
            sb.Append("), .complexform-form:lang(");
            sb.Append(languageKey);
            sb.Append("), .crossref:lang(");
            sb.Append(languageKey);
            sb.Append("), .LexEntry-publishStemComponentTarget-MLHeadWordPub:lang(");
            sb.Append(languageKey);
            sb.Append("), .LexEntry-publishStemMinorPrimaryTarget-MLHeadWordPub:lang(");
            sb.Append(languageKey);
            sb.Append("), .LexSense-publishStemComponentTarget-OwnerOutlinePub:lang(");
            sb.Append(languageKey);
            sb.Append("), .LexSense-publishStemMinorPrimaryTarget-OwnerOutlinePub:lang(");
            sb.Append(languageKey);
            sb.Append("), .sense-crossref:lang(");
            sb.Append(languageKey);
            sb.Append("), .crossref-headword:lang(");
            sb.Append(languageKey);
            sb.Append("), .reversal-form:lang(");
            sb.Append(languageKey);
            sb.Append("), .Alternate_Reading:lang(");
            // scripture
            sb.Append(languageKey);
            sb.Append("), .Section_Head:lang(");
            sb.Append(languageKey);
            sb.Append("), .Section_Head_Minor:lang(");
            sb.Append(languageKey);
            sb.Append("), .Inscription:lang(");
            sb.Append(languageKey);
            sb.Append("), .Intro_Section_Head:lang(");
            sb.Append(languageKey);
            sb.Append("), .Section_Head_Major:lang(");
            sb.Append(languageKey);
            sb.Append("), .iot:lang(");
            sb.Append(languageKey);
            sb.Append("), .revsensenumber:lang(");
            sb.Append(languageKey);
            sb.AppendLine(") {");
            sb.Append("font-family: ");
            sb.Append(IncludeQuoteOnFontName(languageName));
            sb.Append(", ");
            if (_embeddedFonts.TryGetValue(languageName, out embeddedFont))
            {
                sb.AppendLine((embeddedFont.Serif) ? "Times, serif;" : "Arial, sans-serif;");
            }
            else
            {
                // fall back on a serif font if we can't find it (shouldn't happen)
                sb.AppendLine("Times, serif;");
            }
            sb.AppendLine("}");
            return embeddedFont;
        }

        private EmbeddedFont WriteItalicClassesDeclarationBlock(StringBuilder sb, string languageKey, string languageName, EmbeddedFont embeddedFont)
        {
            // dictionary
            sb.Append(".partofspeech:lang(");
            sb.Append(languageKey);
            sb.Append("), .example:lang(");
            sb.Append(languageKey);
            sb.Append("), .grammatical-info:lang(");
            sb.Append(languageKey);
            sb.Append("), .lexref-type:lang(");
            sb.Append(languageKey);
            // scripture
            sb.Append("), .parallel_passage_reference:lang(");
            sb.Append(languageKey);
            sb.Append("), .Parallel_Passage_Reference:lang(");
            sb.Append(languageKey);
            sb.Append("), .Emphasis:lang(");
            sb.Append(languageKey);
            sb.Append("), .pictureCaption:lang(");
            sb.Append(languageKey);
            sb.Append("), .Section_Range_Paragraph:lang(");
            sb.Append(languageKey);
            sb.Append("), .revsensenumber:lang(");
            sb.Append(languageKey);
            sb.AppendLine(") {");
            sb.Append("font-family: ");
            sb.Append(IncludeQuoteOnFontName(languageName));
            sb.Append(", ");
            if (_embeddedFonts.TryGetValue(languageName, out embeddedFont))
            {
                sb.AppendLine((embeddedFont.Serif) ? "Times, serif;" : "Arial, sans-serif;");
            }
            else
            {
                // fall back on a serif font if we can't find it (shouldn't happen)
                sb.AppendLine("Times, serif;");
            }
            sb.AppendLine("}");
            return embeddedFont;
        }

        private EmbeddedFont WriteGenericLanguageDeclarationBlock(StringBuilder sb, string languageKey, string languageName)
        {
            EmbeddedFont embeddedFont;
            sb.Append("*:lang(");
            sb.Append(languageKey);
            sb.AppendLine(") {");
            sb.Append("font-family: ");
            sb.Append(IncludeQuoteOnFontName(languageName));
            sb.Append(", ");
            if (_embeddedFonts.TryGetValue(languageName, out embeddedFont))
            {
                sb.AppendLine((embeddedFont.Serif) ? "Times, serif;" : "Arial, sans-serif;");
            }
            else
            {
                // fall back on a serif font if we can't find it (shouldn't happen)
                sb.AppendLine("Times, serif;");
            }
            // also insert the text direction for this language
            sb.Append("direction: ");
            sb.Append(Common.GetTextDirection(languageKey));
            sb.AppendLine(";");
            sb.AppendLine("}");
            return embeddedFont;
        }

        private static void WriteReversalFontDeclaration(StringBuilder sb, string mainLanguageName, string languageKey, string reverseSenseNumberFont)
        {
            sb.Append(".revsensenumber {");
            sb.Append("font-family: '");
            if (languageKey == reverseSenseNumberFont)
            {
                sb.Append(mainLanguageName);
            }
            sb.Append("';}");
        }

        private EmbeddedFont WriteBoldLanguageFontDeclarationBlock(StringBuilder sb, EmbeddedFont embeddedFont, string mainLanguageName)
        {
            sb.Append(
                ".headword, .headword-minor, .LexSense-publishStemMinorPrimaryTarget-OwnerOutlinePub, ");
            sb.Append(".LexEntry-publishStemMinorPrimaryTarget-MLHeadWordPub, .xsensenumber");
            sb.Append(
                ".complexform-form, .crossref, .LexEntry-publishStemComponentTarget-MLHeadWordPub, ");
            sb.Append(
                ".LexEntry-publishStemMinorPrimaryTarget-MLHeadWordPub, .LexSense-publishStemComponentTarget-OwnerOutlinePub, ");
            sb.Append(".LexSense-publishStemMinorPrimaryTarget-OwnerOutlinePub, .sense-crossref, ");
            sb.Append(".crossref-headword, .reversal-form, ");
            sb.Append(".Alternate_Reading, .Section_Head_Minor, ");
            sb.AppendLine(".Inscription, .Intro_Section_Head, .Section_Head_Major, .iot {");
            if (mainLanguageName.ToLower() == "charis sil")
            {
                sb.Append("font-family: '" + mainLanguageName.Trim());
                sb.Append("-b");
                sb.Append("', ");
            }
            else
            {
                sb.Append("font-family: ");
                sb.Append(IncludeQuoteOnFontName(mainLanguageName));
                sb.Append(", ");
            }
            if (_embeddedFonts.TryGetValue(mainLanguageName, out embeddedFont))
            {
                sb.AppendLine((embeddedFont.Serif) ? "Times, serif;" : "Arial, sans-serif;");
            }
            else
            {
                // fall back on a serif font if we can't find it (shouldn't happen)
                sb.AppendLine("Times, serif;");
            }
            sb.AppendLine("}");
            return embeddedFont;
        }

        private EmbeddedFont WriteItalicLanguageFondDeclarationBlock(StringBuilder sb, EmbeddedFont embeddedFont, string mainLanguageName)
        {
            sb.Append(".partofspeech, .example, .grammatical-info, .lexref-type, ");
            sb.Append(".parallel_passage_reference, .Parallel_Passage_Reference, ");
            sb.AppendLine(".Emphasis, .pictureCaption, .Section_Range_Paragraph {");
            if (mainLanguageName.ToLower() == "charis sil")
            {
                sb.Append("font-family: '" + mainLanguageName.Trim());
                sb.Append("-i");
                sb.Append("', ");
            }
            else
            {
                sb.Append("font-family: ");
                sb.Append(IncludeQuoteOnFontName(mainLanguageName));
                sb.Append(", ");
            }
            if (_embeddedFonts.TryGetValue(mainLanguageName, out embeddedFont))
            {
                sb.AppendLine((embeddedFont.Serif) ? "Times, serif;" : "Arial, sans-serif;");
            }
            else
            {
                // fall back on a serif font if we can't find it (shouldn't happen)
                sb.AppendLine("Times, serif;");
            }
            sb.AppendLine("}");
            return embeddedFont;
        }

        private EmbeddedFont WriteMainLanguageDeclarationBlock(string mainTextDirection, StringBuilder sb, string mainLanguageName)
        {
            EmbeddedFont embeddedFont;
            sb.AppendLine("/* default language font info */");
            sb.AppendLine("body {");
            sb.Append("font-family: ");
            sb.Append(IncludeQuoteOnFontName(mainLanguageName));
            sb.Append(", ");
            if (_embeddedFonts.TryGetValue(mainLanguageName, out embeddedFont))
            {
                sb.AppendLine((embeddedFont.Serif) ? "Times, serif;" : "Arial, sans-serif;");
            }
            else
            {
                // fall back on a serif font if we can't find it (shouldn't happen)
                sb.AppendLine("Times, serif;");
            }
            // also insert the text direction for this language
            sb.Append("direction: ");
            sb.Append(mainTextDirection);
            sb.AppendLine(";");
            sb.AppendLine("}");
            return embeddedFont;
        }

        private static void WriteBoldVariantDeclarationBlock(StringBuilder sb, EmbeddedFont embeddedFont)
        {
            sb.AppendLine("@font-face {");
            sb.Append(" font-family : \"");
            sb.Append(embeddedFont.Name + "\"");
            sb.AppendLine(";");
            sb.AppendLine(" font-weight : bold;");
            sb.AppendLine(" font-style : normal;");
            sb.AppendLine(" font-variant : normal;");
            sb.AppendLine(" font-size : all;");
            sb.Append(" src : url('");
            sb.Append(Path.GetFileName(embeddedFont.BoldFilename));
            sb.AppendLine("');");
            sb.AppendLine("}");
        }

        private static void WriteItalicVariantDeclarationBlock(StringBuilder sb, EmbeddedFont embeddedFont)
        {
            sb.AppendLine("@font-face {");
            sb.Append(" font-family : \"");
            sb.Append(embeddedFont.Name + "\"");
            sb.AppendLine(";");
            sb.AppendLine(" font-weight : normal;");
            sb.AppendLine(" font-style : italic;");
            sb.AppendLine(" font-variant : normal;");
            sb.AppendLine(" font-size : all;");
            sb.Append(" src : url('");
            sb.Append(Path.GetFileName(embeddedFont.ItalicFilename));
            sb.AppendLine("');");
            sb.AppendLine("}");
        }

        private static void WriteFontDeclarationBlock(StringBuilder sb, EmbeddedFont embeddedFont)
        {
            sb.AppendLine("@font-face {");
            sb.Append(" font-family : ");
            sb.Append("\"" + embeddedFont.Name + "\"");
            sb.AppendLine(";");
            sb.AppendLine(" font-weight : normal;");
            sb.AppendLine(" font-style : normal;");
            sb.AppendLine(" font-variant : normal;");
            sb.AppendLine(" font-size : all;");
            sb.Append(" src : url('");
            sb.Append(Path.GetFileName(embeddedFont.Filename));
            sb.AppendLine("');");
            sb.AppendLine("}");
        }

        private static void WriteMissingFontMessage(StringBuilder sb, EmbeddedFont embeddedFont)
        {
            sb.Append("/* missing embedded font: ");
            sb.Append(embeddedFont.Name);
            sb.AppendLine(" */");
        }

        private static void WriteProductNameAndTimeStamp(StringBuilder sb)
        {
            sb.Append("/* font info - added by ");
            sb.Append(Application.ProductName);
            sb.Append(" (");
            sb.Append(Assembly.GetCallingAssembly().FullName);
            sb.AppendLine(") */");
        }


        private string GetLanguageForReversalNumber(string xhtmlFileName, string languageCode)
        {
            string language = languageCode;
            XmlDocument xdoc = Common.DeclareXMLDocument(false);
            var namespaceManager = new XmlNamespaceManager(xdoc.NameTable);
            namespaceManager.AddNamespace("xhtml", "http://www.w3.org/1999/xhtml");
            xdoc.Load(xhtmlFileName);
            // now go check to see if we're working on scripture or dictionary data
            XmlNodeList nodes = xdoc.SelectNodes("//xhtml:span[@class='revsensenumber']", namespaceManager);
            if (nodes == null || nodes.Count == 0)
            {
                nodes = xdoc.SelectNodes("//span[@class='revsensenumber']", namespaceManager);
            }
            if (nodes != null && nodes.Count > 0)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    var xmlAttributeCollection = nodes[i].Attributes;
                    if (xmlAttributeCollection != null)
                        if (xmlAttributeCollection["lang"] != null)
                        {
                            if (xmlAttributeCollection["lang"].Value == language)
                            {
                                language = xmlAttributeCollection["lang"].Value;
                                break;
                            }
                        }

                    if (xmlAttributeCollection != null)
                        if (xmlAttributeCollection["xml:lang"] != null)
                        {
                            if (xmlAttributeCollection["xml:lang"].Value == language)
                            {
                                language = xmlAttributeCollection["xml:lang"].Value;
                                break;
                            }
                        }
                }
            }
            return language;
        }


        /// <summary>
        /// Returns the font families for the languages in _langFontDictionary.
        /// </summary>
        private void BuildFontsList()
        {
            // modifying the _langFontDictionary dictionary - let's make an array copy for the iteration
            int numLangs = _langFontDictionary.Keys.Count;
            var langs = new string[numLangs];
            _langFontDictionary.Keys.CopyTo(langs, 0);
            foreach (var language in langs)
            {
                string[] langCoun = language.Split('-');

                try
                {
                    // When no hyphen use entire value but when there is a hyphen, look for first part
                    var langTarget = langCoun.Length < 2 ? langCoun[0] : language;
                    string wsPath = Common.PathCombine(Common.GetLDMLPath(), langTarget + ".ldml");
                    if (File.Exists(wsPath))
                    {
                        var ldml = Common.DeclareXMLDocument(false);
                        ldml.Load(wsPath);
                        var nsmgr = new XmlNamespaceManager(ldml.NameTable);
                        nsmgr.AddNamespace("palaso", "urn://palaso.org/ldmlExtensions/v1");
                        var node = ldml.SelectSingleNode("//palaso:defaultFontFamily/@value", nsmgr);
                        if (node != null)
                        {
                            // build the font information and return
                            _langFontDictionary[language] = node.Value; // set the font used by this language
                            _embeddedFonts[node.Value] = new EmbeddedFont(node.Value);
                        }
                    }
                    else if (AppDomain.CurrentDomain.FriendlyName.ToLower() == "paratext.exe") // is paratext
                    {
                        var settingsHelper = new SettingsHelper(Param.DatabaseName);
                        string fileName = settingsHelper.GetSettingsFilename();
                        const string xPath = "//ScriptureText/DefaultFont";
                        XmlNode xmlFont = Common.GetXmlNode(fileName, xPath);
                        if (xmlFont != null)
                        {
                            // get the text direction specified by the .ssf file
                            _langFontDictionary[language] = xmlFont.InnerText; // set the font used by this language
                            _embeddedFonts[xmlFont.InnerText] = new EmbeddedFont(xmlFont.InnerText);
                        }
                    }
                    else
                    {
                        // Paratext case (no .ldml file) - fall back on Charis
                        _langFontDictionary[language] = "Charis SIL"; // set the font used by this language
                        _embeddedFonts["Charis SIL"] = new EmbeddedFont("Charis SIL");

                    }
                }
                catch
                {
                }
            }
        }

        #endregion

        #region Language Handling
        /// <summary>
        /// Parses the specified file and sets the internal languages list to all the languages found in the file.
        /// </summary>
        /// <param name="xhtmlFileName">File name to parse</param>
        private void BuildLanguagesList(string xhtmlFileName)
        {
            XmlDocument xmlDocument = Common.DeclareXMLDocument(false);
            var namespaceManager = new XmlNamespaceManager(xmlDocument.NameTable);
            namespaceManager.AddNamespace("xhtml", "http://www.w3.org/1999/xhtml");
            var xmlReaderSettings = new XmlReaderSettings { XmlResolver = null, ProhibitDtd = false }; //Common.DeclareXmlReaderSettings(false);
            var xmlReader = XmlReader.Create(xhtmlFileName, xmlReaderSettings);
            xmlDocument.Load(xmlReader);
            xmlReader.Close();
            // should only be one of these after splitting out the chapters.
            XmlNodeList nodes = xmlDocument.SelectNodes("//@lang", namespaceManager);
            if (nodes != null && nodes.Count > 0)
            {
                foreach (XmlNode node in nodes)
                {
                    string value;
                    if (_langFontDictionary.TryGetValue(node.Value, out value))
                    {
                        // already have this item in our list - continue
                        continue;
                    }
                    if (node.Value.ToLower() == "utf-8")
                    {
                        // TE-9078 "utf-8" showing up as language in html tag - remove when fixed
                        continue;
                    }
                    // add an entry for this language in the list (the * gets overwritten in BuildFontsList())
                    _langFontDictionary.Add(node.Value, "*");
                }
            }
            // now go check to see if we're working on scripture or dictionary data
            nodes = xmlDocument.SelectNodes("//xhtml:span[@class='headword']", namespaceManager);

            if (nodes == null || nodes.Count == 0)
            {
                nodes = xmlDocument.SelectNodes("//span[@class='headword']", namespaceManager);
            }
            if (nodes != null && nodes.Count == 0)
            {
                // not in this file - this might be scripture?
                nodes = xmlDocument.SelectNodes("//xhtml:span[@class='scrBookName']", namespaceManager);
                if (nodes == null || nodes.Count == 0)
                {
                    nodes = xmlDocument.SelectNodes("//span[@class='scrBookName']", namespaceManager);
                }
                if (nodes != null && nodes.Count > 0)
                    InputType = "scripture";
            }
            else
            {
                InputType = "dictionary";
            }
        }

        #endregion

        #region Book ID and Name
        /// <summary>
        /// Returns a book ID to be used in the .opf file. This is similar to the GetBookName call, but here
        /// we're wanting something that (1) doesn't start with a numeric value and (2) is unique.
        /// </summary>
        /// <param name="xhtmlFileName"></param>
        /// <returns></returns>
        private string GetBookId(string xhtmlFileName)
        {
            try
            {
                XmlDocument xmlDocument = Common.DeclareXMLDocument(false);
                var namespaceManager = new XmlNamespaceManager(xmlDocument.NameTable);
                namespaceManager.AddNamespace("xhtml", "http://www.w3.org/1999/xhtml");
                var xmlReaderSettings = new XmlReaderSettings { XmlResolver = null, ProhibitDtd = false }; //Common.DeclareXmlReaderSettings(false);
                var xmlReader = XmlReader.Create(xhtmlFileName, xmlReaderSettings);
                xmlDocument.Load(xmlReader);
                xmlReader.Close();
                // should only be one of these after splitting out the chapters.
                XmlNodeList nodes;
                if (InputType.Equals("dictionary"))
                {
                    nodes = xmlDocument.SelectNodes("//xhtml:div[@class='letter']", namespaceManager);
                }
                else
                {
                    // no scrBookName - use Title_Main
                    nodes = xmlDocument.SelectNodes("//xhtml:div[@class='Title_Main']", namespaceManager);
                    if (nodes == null || nodes.Count == 0)
                    {
                        // start out with the book code (e.g., 2CH for 2 Chronicles)
                        nodes = xmlDocument.SelectNodes("//xhtml:span[@class='scrBookCode']", namespaceManager);
                    }
                    if (nodes == null || nodes.Count == 0)
                    {
                        // no book code - use scrBookName
                        nodes = xmlDocument.SelectNodes("//xhtml:span[@class='scrBookName']", namespaceManager);
                    }
                }
                if (nodes != null && nodes.Count > 0)
                {
                    var sb = new StringBuilder();
                    // just in case the name starts with a number, prepend "id"
                    sb.Append("id");
                    // remove any whitespace in the node text (the ID can't have it)
                    sb.Append("boooknode");
                    return (sb.ToString());
                }
                // fall back on just the file name
                return Path.GetFileName(xhtmlFileName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                if (ex.StackTrace != null)
                {
                    Debug.WriteLine(ex.StackTrace);
                }
                return Path.GetFileName(xhtmlFileName);
            }
        }

        /// <summary>
        /// Returns the user-friendly book name inside this file.
        /// </summary>
        /// <param name="xhtmlFileName">Split xhtml filename in the form PartFile[#]_.xhtml</param>
        /// <returns>User-friendly book name (value of the scrBookName or letter element in the xhtml file).</returns>
        private string GetBookName(string xhtmlFileName)
        {
            var fileNoPath = Path.GetFileName(xhtmlFileName);
            if (fileNoPath != null && fileNoPath.StartsWith(PreExportProcess.CoverPageFilename.Substring(0, 8)))
            {
                return ("Cover Page");
            }
            if (fileNoPath != null && fileNoPath.StartsWith(PreExportProcess.TitlePageFilename.Substring(0, 8)))
            {
                return ("Title Page");
            }
            if (fileNoPath != null && fileNoPath.StartsWith(PreExportProcess.TableOfContentsFilename.Substring(0, 8)))
            {
                return ("Table of Content");
            }
            if (fileNoPath != null && fileNoPath.StartsWith(PreExportProcess.CopyrightPageFilename.Substring(0, 8)))
            {
                return ("Copyright Information");
            }
            XmlDocument xmlDocument = Common.DeclareXMLDocument(false);
            var namespaceManager = new XmlNamespaceManager(xmlDocument.NameTable);
            namespaceManager.AddNamespace("xhtml", "http://www.w3.org/1999/xhtml");
            var xmlReaderSettings = new XmlReaderSettings { XmlResolver = null, ProhibitDtd = false }; //Common.DeclareXmlReaderSettings(false);
            var xmlReader = XmlReader.Create(xhtmlFileName, xmlReaderSettings);
            xmlDocument.Load(xmlReader);
            xmlReader.Close();
            // should only be one of these after splitting out the chapters.
            XmlNodeList nodes;
            if (InputType.Equals("dictionary"))
            {
                nodes = xmlDocument.SelectNodes("//xhtml:div[@class='letter']", namespaceManager);
                if (nodes == null || nodes.Count == 0)
                {
                    nodes = xmlDocument.SelectNodes("//div[@class='letter']", namespaceManager);
                }
            }
            else
            {
                nodes = xmlDocument.SelectNodes("//xhtml:span[@class='scrBookName']", namespaceManager);
                if (nodes == null || nodes.Count == 0)
                {
                    nodes = xmlDocument.SelectNodes("//span[@class='scrBookName']", namespaceManager);
                }
                if (nodes == null || nodes.Count == 0)
                {
                    // nothing there - check on the Title_Main span
                    nodes = xmlDocument.SelectNodes("//xhtml:div[@class='Title_Main']", namespaceManager);
                    // nothing there - check on the scrBookName span
                    if (nodes == null || nodes.Count == 0)
                    {
                        nodes = xmlDocument.SelectNodes("//div[@class='Title_Main']", namespaceManager);
                    }
                }
                if (nodes == null || nodes.Count == 0)
                {
                    // we're really scraping the bottom - check on the scrBookCode span
                    nodes = xmlDocument.SelectNodes("//xhtml:span[@class='scrBookCode']", namespaceManager);
                    if (nodes == null || nodes.Count == 0)
                    {
                        nodes = xmlDocument.SelectNodes("//span[@class='scrBookCode']", namespaceManager);
                    }
                }
            }
            if (nodes != null && nodes.Count > 0)
            {
                var sb = new StringBuilder();
                sb.Append(nodes[0].InnerText);
                return (sb.ToString());
            }
            // fall back on just the file name
            return Path.GetFileName(xhtmlFileName);
        }
        #endregion

        #region Relative Hyperlink processing
        /// <summary>
        /// Returns the list of "broken" relative hyperlink hrefs in the given file (i.e.,
        /// relative hyperlinks that don't have a target within the file). This can happen when
        /// the xhtml file gets split out into multiple pieces, and the target for an href ends up
        /// in a different file.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private IEnumerable<string> FindBrokenRelativeHrefIds(string filePath)
        {
            if (!File.Exists(filePath)) return null;
            const string searchText = "a href=\"#"; // denotes a relative href
            var brokenRelativeHrefIds = new List<string>();
            var reader = new StreamReader(filePath);
            var content = reader.ReadToEnd();
            reader.Close();
            int start = content.IndexOf(searchText, 0, StringComparison.Ordinal);
            if (start != -1)
            {
                start += searchText.Length;
            }
            while (start != -1)
            {
                // next instance of a relative hyperlink ref - read until the closing quote
                int stop = (content.IndexOf("\"", start, StringComparison.Ordinal) - start);
                if (stop == -1) { break; }
                var hrefId = content.Substring(start, (stop));
                // not found -- this link is broken
                if (!brokenRelativeHrefIds.Contains(hrefId))
                {
                    brokenRelativeHrefIds.Add(hrefId);
                }
                start = content.IndexOf(searchText, (start + stop), StringComparison.Ordinal);
                if (start != -1)
                {
                    start += searchText.Length;
                }
            }
            return brokenRelativeHrefIds;
        }

        private void FixRelativeHyperlinks(string contentFolder)
        {
            string[] files = Directory.GetFiles(contentFolder, "PartFile*.xhtml");
            string[] revFiles = Directory.GetFiles(contentFolder, "RevIndex*.xhtml");
            //inProcess.AddToMaximum(files.Length);
            var preExport = new PreExportProcess();
            var dictHyperlinks = new Dictionary<string, string>();
            var sourceList = new List<string>();
            var targetList = new List<string>();
            var targettempList = new List<string>();
            var fileDict = new Dictionary<string, string>();

            foreach (string targetFile in files)
            {
                preExport.GetReferenceList(targetFile, sourceList, targettempList);

                targetList.AddRange(targettempList);
                foreach (string target in targettempList)
                {
                    fileDict[target] = Path.GetFileName(targetFile);
                }
                targettempList.Clear();
            }

            foreach (string target in targetList)
            {
                if (sourceList.Contains(target) && !dictHyperlinks.ContainsKey(target))
                {
                    dictHyperlinks.Add(target, fileDict[target] + "#" + target);
                }
            }

            if (dictHyperlinks.Count > 0)
            {
                foreach (string targetFile in files)
                {
                    RemoveSpanVerseNumberNodeInXhtmlFile(targetFile);
                    ReplaceAllBrokenHrefs(targetFile, dictHyperlinks);
                }
                foreach (string targetFile in revFiles)
                {
                    RemoveSpanVerseNumberNodeInXhtmlFile(targetFile);
                    ReplaceAllBrokenHrefs(targetFile, dictHyperlinks);
                }
            }
            else
            {
                if (files.Length > 0)
                {
                    foreach (string targetFile in files)
                    {
                        RemoveSpanVerseNumberNodeInXhtmlFile(targetFile);
                    }
                }
                if (revFiles.Length > 0)
                {
                    foreach (string targetFile in revFiles)
                    {
                        RemoveSpanVerseNumberNodeInXhtmlFile(targetFile);
                    }
                }
            }
        }

        private void RemoveSpanVerseNumberNodeInXhtmlFile(string fileName)
        {
            //Removed NoteTargetReference tag from XHTML file
            XmlDocument xDoc = Common.DeclareXMLDocument(true);
            var namespaceManager = new XmlNamespaceManager(xDoc.NameTable);
            namespaceManager.AddNamespace("xhtml", "http://www.w3.org/1999/xhtml");
            xDoc.Load(fileName);
            var elmRoot = xDoc.DocumentElement;

            //If includeImage is false, removes the img -> parent tag

            const string xPath = "//xhtml:div[@class='scrBook']/xhtml:span[@class='Verse_Number']";
            if (elmRoot != null)
            {
                XmlNodeList divNode = elmRoot.SelectNodes(xPath, namespaceManager);
                if (divNode != null && divNode.Count > 0)
                {
                    for (int i = 0; i < divNode.Count; i++)
                    {
                        var parentNode = divNode[i].ParentNode;
                        if (parentNode != null)
                            parentNode.RemoveChild(divNode[i]);
                    }
                }
            }

            xDoc.Save(fileName);
        }

        private void ReplaceAllBrokenHrefs(string filePath, Dictionary<string, string> dictHyperlinks)
        {
            if (!File.Exists(filePath)) return;
            var reader = new StreamReader(filePath);
            string content = reader.ReadToEnd();
            reader.Close();
            var contentWriter = new StringBuilder();
            const string searchText = "a href=\"#";
            int startIndex = 0;
            bool done = false;
            while (!done)
            {
                int nextIndex = content.IndexOf(searchText, startIndex, StringComparison.Ordinal);
                if (nextIndex >= 0)
                {
                    // find the href target
                    int stopIndex = content.IndexOf("\"", nextIndex + searchText.Length, StringComparison.Ordinal);
                    var target = content.Substring(nextIndex + searchText.Length, stopIndex - (nextIndex + searchText.Length));
                    // is it in our dictionary?
                    string newValue;
                    if (dictHyperlinks.TryGetValue(target, out newValue))
                    {
                        // yes - write the corrected text to the output file
                        contentWriter.Append(content.Substring(startIndex, nextIndex - startIndex));
                        contentWriter.Append("a href=\"");
                        contentWriter.Append(newValue);
                    }
                    else
                    {
                        // no - write out the existing text to the output file
                        contentWriter.Append(content.Substring(startIndex, stopIndex - startIndex));
                    }
                    // update startIndex
                    startIndex = stopIndex;
                }
                else
                {
                    // no more relative hyperlinks
                    contentWriter.Append(content.Substring(startIndex, content.Length - startIndex));
                    done = true;
                }
            }
            var writer = new StreamWriter(filePath);
            writer.Write(contentWriter);
            writer.Close();
        }
        #endregion

        #region Image processing
        /// <summary>
        /// This method handles the images for the .epub file. Each image is resized and renamed (to .png) if necessary, then
        /// copied to the .epub folder. Any references to the image files from the .xhtml are also updated if needed.
        /// </summary>
        /// <param name="tempFolder"></param>
        /// <param name="contentFolder"></param>
        private void ProcessImages(string tempFolder, string contentFolder)
        {
            string[] imageFiles = Directory.GetFiles(tempFolder);
            bool renamedImages = false;
            foreach (string file in imageFiles)
            {
                Image image;
// ReSharper disable PossibleNullReferenceException
                switch (Path.GetExtension(file) == null ? "" : Path.GetExtension(file).ToLower())
// ReSharper restore PossibleNullReferenceException
                {
                    case ".jpg":
                    case ".jpeg":
                    case ".gif":
                    case ".png":
                        // .epub supports this image format - just copy the thing over
                        string name = Path.GetFileName(file);
                        string dest = Common.PathCombine(contentFolder, name);
                        // sanity check - if the image is gigantic, scale it
                        image = Image.FromFile(file);
                        if (image.Width > MaxImageWidth)
                        {
                            // need to scale image
                            var img = ResizeImage(image);
                            var extension = Path.GetExtension(file);
                            if (extension != null)
                                switch (extension.ToLower())
                                {
                                    case ".jpg":
                                    case ".jpeg":
                                        img.Save(dest, System.Drawing.Imaging.ImageFormat.Jpeg);
                                        break;
                                    case ".gif":
                                        img.Save(dest, System.Drawing.Imaging.ImageFormat.Gif);
                                        break;
                                    default:
                                        img.Save(dest, System.Drawing.Imaging.ImageFormat.Png);
                                        break;
                                }
                        }
                        else
                        {
                            File.Copy(file, dest);
                        }
                        break;
                    case ".bmp":
                    case ".tif":
                    case ".tiff":
                    case ".ico":
                    case ".wmf":
                    case ".pcx":
                    case ".cgm":
                        // TE (and others?) support these file types, but .epub doesn't -
                        // convert them to .png if we can
                        var imageName = Path.GetFileNameWithoutExtension(file) + ".png";
                        using (var fileStream = new FileStream(Common.PathCombine(contentFolder, imageName), FileMode.CreateNew))
                        {
                            image = Image.FromFile(file);
                            if (image.Width > MaxImageWidth)
                            {
                                var img = ResizeImage(image);
                                img.Save(fileStream, System.Drawing.Imaging.ImageFormat.Png);
                            }
                            else
                            {
                                image.Save(fileStream, System.Drawing.Imaging.ImageFormat.Png);
                            }
                        }
                        renamedImages = true;
                        break;
                }
            }
            // be sure to clean up any hyperlink references to the old file types
            if (renamedImages)
            {
                CleanupImageReferences(contentFolder);
            }
        }

        /// <summary>
        /// Resizes the given image down to MaxImageWidth pixels and returns the result.
        /// </summary>
        /// <param name="image">File to resize</param>
        private Image ResizeImage(Image image)
        {
            float nPercent = ((float)MaxImageWidth / (float)image.Width);
            var destW = (int)(image.Width * nPercent);
            var destH = (int)(image.Height * nPercent);
            var b = new Bitmap(destW, destH);
            var g = Graphics.FromImage((Image)b);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(image, 0, 0, destW, destH);
            //g.Dispose();
            return (Image)b;
        }

        /// <summary>
        /// The .epub format doesn't support all image file types; when we copied the image files over, we had
        /// to convert the unsupported file types to .png. Here we'll do a search/replace for all references to
        /// the old versions.
        /// </summary>
        /// <param name="contentFolder">OEBPS folder containing all the xhtml files we need to clean up</param>
        private void CleanupImageReferences(string contentFolder)
        {
            string[] files = Directory.GetFiles(contentFolder, "*.xhtml");
            foreach (string file in files)
            {
                // using a streaming approach to reduce the memory footprint of this method
                // (we had Regex.Replace before, but it was using >100MB of data on larger dictionaries)
                var reader = new StreamReader(file);
                var writer = new StreamWriter(file + ".tmp");
                Int32 next;
                while ((next = reader.Read()) != -1)
                {
                    var b = (char)next;
                    if (b == '.') // found a period - is it a filename extension that we need to change?
                    {
                        // copy the period and the next 3 characters into a string
                        const int len = 4;
                        var buf = new char[len];
                        buf[0] = b;
                        reader.Read(buf, 1, 3);
                        var data = new string(buf);
                        // is this an unsupported filename extension?
                        switch (data)
                        {
                            case ".bmp":
                            case ".ico":
                            case ".wmf":
                            case ".pcx":
                            case ".cgm":
                                // yes - replace with ".png"
                                writer.Write(".png");
                                break;
                            case ".tif":
                                // yes, but this could be either ".tif" or ".tiff" -
                                // find out which one by peeking at the next character
                                int nextchar = reader.Peek();
                                if (((char)nextchar) == 'f')
                                {
                                    // ".tiff" case
                                    reader.Read(); // move the reader up one position (consume the "f")
                                    // replace with ".png"
                                    writer.Write(".png");
                                }
                                else
                                {
                                    // ".tif" case - replace it with ".png"
                                    writer.Write(".png");
                                }
                                break;
                            default:
                                // not an unsupported extension - just write the data we collected
                                writer.Write(data);
                                break;
                        }
                    }
                    else // not a "."
                    {
                        writer.Write((char)next);
                    }
                }
                reader.Close();
                writer.Close();
                // replace the original file with the new one
                File.Delete(file);
                File.Move((file + ".tmp"), file);
            }
        }
        #endregion

        #region File Processing Methods
        /// <summary>
        /// Returns true if the specified search text string is found in the given file.
        /// Modified to use the Knuth-Morris-Pratt search algorithm 
        /// (http://en.wikipedia.org/wiki/Knuth%E2%80%93Morris%E2%80%93Pratt_algorithm)
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="searchText"></param>
        /// <returns></returns>
        private bool IsStringInFile(string filePath, string searchText)
        {

            try
            {
                XmlTextReader reader = Common.DeclareXmlTextReader(filePath, true);
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        string idString = searchText.Replace("id=\"", "").Replace("\"", "");
                        if (reader.Name == "div" || reader.Name == "span")
                        {

                            string id = reader.GetAttribute("id");
                            if (id == idString)
                            {
                                reader.Close();
                                return true;
                            }
                        }
                    }
                }
                reader.Close();
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Helper method to change the relative hyperlinks in the references file to absolute ones. 
        /// This is done after the scripture files are split out into individual books of 100K or less in size.
        /// </summary>
        /// <param name="contentFolder"></param>
        /// <param name="inProcess"></param>
        private void UpdateReferenceHyperlinks(string contentFolder, InProcess inProcess)
        {
            var outFilename = Common.PathCombine(contentFolder, ReferencesFilename);
            var hrefs = FindBrokenRelativeHrefIds(outFilename);
            //inProcess.AddToMaximum(hrefs.Count + 1);
            var reader = new StreamReader(outFilename);
            var content = new StringBuilder();
            content.Append(reader.ReadToEnd());
            reader.Close();
            string[] files = Directory.GetFiles(contentFolder, "PartFile*.xhtml");
            int index = 0;
            bool looped = false;
            foreach (var href in hrefs)
            {
                // find where the target is for this reference -
                // since the lists are sequential in the references file, we're using an index instead
                // of a foreach loop (so the search continues in the same file the last href left off on).
                while (true)
                {
                    // search the current file in the list
                    if (IsStringInFile(files[index], href))
                    {
                        content.Replace(("a href=\"#" + href + "\""),
                                        ("a href=\"" + Path.GetFileName(files[index]) + "#" + href + "\""));
                        break;
                    }
                    // update the index and try again
                    index++;
                    if (index == files.Length)
                    {
                        if (looped) break; // already searched through the list -- this item isn't found, get out
                        index = 0;
                        looped = true;
                    }
                }
                inProcess.PerformStep();
                looped = false;
            }
            var writer = new StreamWriter(outFilename);
            writer.Write(content);
            writer.Close();
            inProcess.PerformStep();
        }

        /// <summary>
        /// Helper method to change the relative hyperlinks in the references file to absolute ones. 
        /// This is done after the scripture files are split out into individual books of 100K or less in size.
        /// </summary>
        /// <param name="contentFolder"></param>
        /// <param name="inProcess"></param>
        private void UpdateReferenceSourcelinks(string contentFolder, InProcess inProcess)
        {
            string[] files = Directory.GetFiles(contentFolder, "PartFile*.xhtml");
            foreach (var file in files)
            {
                XmlDocument xmlDocument = Common.DeclareXMLDocument(false);
                var namespaceManager = new XmlNamespaceManager(xmlDocument.NameTable);
                namespaceManager.AddNamespace("xhtml", "http://www.w3.org/1999/xhtml");
                var xmlReaderSettings = new XmlReaderSettings { XmlResolver = null, ProhibitDtd = false };
                var xmlReader = XmlReader.Create(file, xmlReaderSettings);
                xmlDocument.Load(xmlReader);
                xmlReader.Close();
                var footnoteNodes = xmlDocument.SelectNodes("//xhtml:span[@class='Note_General_Paragraph']/xhtml:a", namespaceManager);
                if (footnoteNodes == null)
                {
                    return;
                }
                foreach (XmlNode footnoteNode in footnoteNodes)
                {
                    if (footnoteNode.Attributes != null)
                        footnoteNode.Attributes["href"].Value = "zzReferences.xhtml" + footnoteNode.Attributes["href"].Value;
                }

                footnoteNodes = xmlDocument.SelectNodes("//xhtml:span[@class='Note_CrossHYPHENReference_Paragraph']/xhtml:a", namespaceManager);
                if (footnoteNodes == null)
                {
                    return;
                }
                foreach (XmlNode footnoteNode in footnoteNodes)
                {
                    if (footnoteNode.Attributes != null)
                        footnoteNode.Attributes["href"].Value = "zzReferences.xhtml" + footnoteNode.Attributes["href"].Value;
                }

                xmlDocument.Save(file);
                inProcess.PerformStep();
            }
        }

        /// <summary>
        /// Creates a separate references file at the end of the xhtml files in scripture content, for both footnotes and cross-references.
        /// Each reference links back relatively to the source xhtml, so that the links can be updated when the content is split into
        /// smaller chunks.
        /// </summary>
        /// <param name="outputFolder"></param>
        /// <param name="xhtmlFileName"></param>
        private void CreateReferencesFile(string outputFolder, string xhtmlFileName)
        {
            // sanity check - return if the references are to be left in the text
            if (References.Contains("Section")) { return; }
            // collect all cross-references and footnotes in the content file
            XmlDocument xmlDocument = Common.DeclareXMLDocument(false);
            var namespaceManager = new XmlNamespaceManager(xmlDocument.NameTable);
            namespaceManager.AddNamespace("xhtml", "http://www.w3.org/1999/xhtml");
            var xmlReaderSettings = new XmlReaderSettings { XmlResolver = null, ProhibitDtd = false }; //Common.DeclareXmlReaderSettings(false);
            var xmlReader = XmlReader.Create(xhtmlFileName, xmlReaderSettings);
            xmlDocument.Load(xmlReader);
            xmlReader.Close();
            // pick your nodes
            var crossRefNodes = xmlDocument.SelectNodes("//xhtml:span[@class='Note_CrossHYPHENReference_Paragraph']", namespaceManager);
            var footnoteNodes = xmlDocument.SelectNodes("//xhtml:span[@class='Note_General_Paragraph']", namespaceManager);
            if (crossRefNodes == null && footnoteNodes == null)
            {
                // nothing to pull out -- just exit
                return;
            }
            // file preamble
            var sbPreamble = new StringBuilder();
            sbPreamble.Append("<?xml version='1.0' encoding='utf-8'?><!DOCTYPE html PUBLIC '-//W3C//DTD XHTML 1.1//EN' 'http://www.w3.org/TR/xhtml11/DTD/xhtml11.dtd'>");
            sbPreamble.Append("<html xmlns='http://www.w3.org/1999/xhtml'><head><title>");
            sbPreamble.AppendLine("</title><link rel='stylesheet' href='book.css' type='text/css' /></head>");
            sbPreamble.Append("<body class='scrBody'><div class='Front_Matter'>");
            var outFilename = Common.PathCombine(outputFolder, ReferencesFilename);
            var outFile = new StreamWriter(outFilename);
            outFile.WriteLine(sbPreamble.ToString());
            // iterate through the files and pull out each reference hyperlink
            if (footnoteNodes != null && footnoteNodes.Count > 0)
            {
                outFile.WriteLine("<h1>Endnotes</h1>");
                outFile.WriteLine("<ul>");
                foreach (XmlNode footnoteNode in footnoteNodes)
                {
                    outFile.Write("<li id=\"FN_");
                    WriteAttributeValue(outFile, footnoteNode, "id");
                    outFile.Write("\"><a href=\"");
                    outFile.Write("#");
                    WriteAttributeValue(outFile, footnoteNode, "id");
                    outFile.Write("\">[");
                    WriteAttributeValue(outFile, footnoteNode, "title");
                    outFile.Write("] ");
                    outFile.Write("</a> ");
                    XmlNode bookNode = footnoteNode.SelectSingleNode("preceding::xhtml:span[@class='scrBookName'][1]", namespaceManager);
                    if (bookNode != null)
                    {
                        outFile.Write("<span class=\"BookName\"><b>" + bookNode.InnerText + "</b></span>");
                    }
                    outFile.Write(" ");
                    outFile.Write(CleanupSpans(footnoteNode.InnerXml));
                    outFile.WriteLine("</li>");
                }
                outFile.WriteLine("</ul>");
            }
            if (crossRefNodes != null && crossRefNodes.Count > 0)
            {
                outFile.WriteLine("<h1>References</h1>");
                outFile.WriteLine("<ul>");
                foreach (XmlNode crossRefNode in crossRefNodes)
                {
                    outFile.Write("<li id=\"FN_");
                    WriteAttributeValue(outFile, crossRefNode, "id");
                    outFile.Write("\"><a href=\"");
                    outFile.Write("#");
                    WriteAttributeValue(outFile, crossRefNode, "id");
                    outFile.Write("\">");
                    XmlNode bookNode = crossRefNode.SelectSingleNode("preceding::xhtml:span[@class='Title_Main'][1]", namespaceManager);
                    if (bookNode != null)
                    {
                        outFile.Write(bookNode.InnerText);
                    }
                    outFile.Write(" ");
                    XmlNode chapterNode = crossRefNode.SelectSingleNode("preceding::xhtml:span[@class='Chapter_Number'][1]", namespaceManager);
                    if (chapterNode != null)
                    {
                        outFile.Write(chapterNode.InnerText);
                    }
                    outFile.Write(":");
                    XmlNode verseNode = crossRefNode.SelectSingleNode("preceding::xhtml:span[@class='Verse_Number'][1]", namespaceManager);
                    if (verseNode != null)
                    {
                        outFile.Write(verseNode.InnerText);
                    }
                    outFile.Write("</a> ");
                    outFile.Write(CleanupSpans(crossRefNode.InnerXml));
                    outFile.WriteLine("</li>");
                }
                outFile.WriteLine("</ul>");
            }

            outFile.WriteLine("</div></body></html>");
            outFile.Flush();
            outFile.Close();
        }

        private static void WriteAttributeValue(StreamWriter outFile, XmlNode footnoteNode, string attributeName)
        {
            Debug.Assert(outFile != null);
            if (footnoteNode == null || footnoteNode.Attributes == null)
            {
                Debug.WriteLine("No Footnote atribute {0}", attributeName);
            }
            else
            {
                try
                {
                    outFile.Write(footnoteNode.Attributes[attributeName].Value);
                }
                catch (NullReferenceException e)
                {
                    Debug.WriteLine(e.Message);
                }
            }
        }

        private string CleanupSpans(string text)
        {
            var sb = new StringBuilder(text);
            sb.Replace(" lang", " xml:lang");
            sb.Replace("xmlns=\"http://www.w3.org/1999/xhtml\"", "");
            return sb.ToString();
        }

        /// <summary>
        /// Splits the specified xhtml file out into multiple files, either based on letter (dictionary) or book (scripture). 
        /// This method was adapted from ExportOpenOffice.cs.
        /// </summary>
        /// <param name="temporaryCvFullName"></param>
        /// <param name="pubInfo"></param>
        /// <returns></returns>
        private IEnumerable<string> SplitFile(string temporaryCvFullName, PublicationInformation pubInfo)
        {
            List<string> fileNameWithPath;
            if (InputType.Equals("dictionary"))
            {
                fileNameWithPath = Common.SplitXhtmlFile(temporaryCvFullName, "letHead", true);
            }
            else
            {
                fileNameWithPath = Common.SplitXhtmlFile(temporaryCvFullName, "scrBook", false);
            }
            return fileNameWithPath;
        }

        /// <summary>
        /// Splits a book file into smaller files, based on file size.
        /// </summary>
        /// <param name="xhtmlFilename">file to split into smaller pieces</param>
        /// <returns></returns>
        private List<string> SplitBook(string xhtmlFilename)
        {
            const long maxSize = 204800; // 200KB
            // sanity check - make sure the file exists
            if (!File.Exists(xhtmlFilename))
            {
                return null;
            }
            var fileNames = new List<string>();
            // is it worth splitting this file?
            var fi = new FileInfo(xhtmlFilename);
            if (fi.Length <= maxSize)
            {
                // not worth splitting this file - just return it
                fileNames.Add(xhtmlFilename);
                return fileNames;
            }

            // If we got here, it's worth our time to split the file out.
            var reader = new StreamReader(xhtmlFilename);
            string content = reader.ReadToEnd();
            reader.Close();

            string bookcode = "<span class=\"scrBookCode\">" + GetBookId(xhtmlFilename) + "</span>";
            string head = content.Substring(0, content.IndexOf("<body", StringComparison.Ordinal));
            var startIndex = 0;
            var fileIndex = 1;
            var sb = new StringBuilder();
            while (true)
            {
                // look for a good breaking point after our soft maximum size
                var outFile = Common.PathCombine(Path.GetDirectoryName(xhtmlFilename), (Path.GetFileNameWithoutExtension(xhtmlFilename) + fileIndex.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0') + ".xhtml"));
                int softMax = startIndex + (int)(maxSize / 2);
                int realMax;
                if (softMax > content.Length)
                {
                    realMax = -1;
                }
                else
                {
                    var divClass = InputType == "scripture" ? "Section_Head" : "entry";
                    var target = string.Format("<div class=\"{0}", divClass);
                    realMax = content.IndexOf(target, softMax, StringComparison.Ordinal);
                }
                StreamWriter writer;
                if (realMax == -1)
                {
                    if (startIndex == 0)
                    {
                        // can't split this file (no section breaks after the soft limit) - just return it
                        fileNames.Add(xhtmlFilename);
                        return fileNames;
                    }
                    // no more section heads - just pull in the rest of the content
                    // write out head + substring(startIndex to the end)
                    sb.Append(head);
                    if (InputType == "scripture")
                    {
                        sb.Append("<body class=\"scrBody\"><div class=\"scrBook\">");
                        sb.Append(bookcode);
                    }
                    else
                    {
                        sb.Append("<body class=\"dicBody\"><div class=\"letData\">");
                    }

                    sb.AppendLine(content.Substring(startIndex));
                    writer = new StreamWriter(outFile);
                    writer.Write(sb.ToString());
                    writer.Close();
                    // add this file to fileNames)))
                    fileNames.Add(outFile);
                    break;
                }
                // build the content
                if (startIndex == 0)
                {
                    // for the first section, we go from the start of the file to realMax
                    sb.Append(content.Substring(0, (realMax - startIndex)));
                    sb.AppendLine("</div></body></html>"); // close out the xhtml
                }
                else
                {
                    // for the subsequent sections, we need the head + the substring (startIndex to realMax)
                    sb.Append(head);
                    var bodyClass = InputType == "scripture"? "scrBody": "dicBody";
                    var divClass = InputType == "scripture"? "scrBook": "letData";
                    sb.Append(string.Format("<body class=\"{0}\"><div class=\"{1}\">", bodyClass, divClass));
                    sb.Append(content.Substring(startIndex, (realMax - startIndex)));
                    sb.AppendLine("</div></body></html>"); // close out the xhtml
                }
                // write the string buffer content out to file
                writer = new StreamWriter(outFile);
                writer.Write(sb.ToString());
                writer.Close();
                // add this file to fileNames
                fileNames.Add(outFile);
                // move the indices up for the next file chunk
                startIndex = realMax;
                // reset the stringbuilder
                sb.Length = 0;
                fileIndex++;
            }
            // return the result
            return fileNames;
        }

        /// <summary>
        /// Copies the selected source folder and its subdirectories to the destination folder path. 
        /// This is a recursive method.
        /// </summary>
        /// <param name="sourceFolder"></param>
        /// <param name="destFolder"></param>
        private void CopyFolder(string sourceFolder, string destFolder)
        {
            if (Directory.Exists(destFolder))
            {
                var di = new DirectoryInfo(destFolder);
                Common.CleanDirectory(di);
            }
            Directory.CreateDirectory(destFolder);
            string[] files = Directory.GetFiles(sourceFolder);
            try
            {
                foreach (string file in files)
                {
                    string name = Path.GetFileName(file);
                    string dest = Common.PathCombine(destFolder, name);
                    // Special processing for the mimetype file - don't copy it now; copy it after
                    // compressing the rest of the archive (in Compress() below) as a stored / not compressed
                    // file in the archive. This is keeping in line with the .epub OEBPS Container Format (OCF)
                    // recommendations: http://www.idpf.org/ocf/ocf1.0/download/ocf10.htm.
                    if (name != "mimetype")
                    {
                        File.Copy(file, dest);
                    }
                }

                string[] folders = Directory.GetDirectories(sourceFolder);
                foreach (string folder in folders)
                {
                    string name = Path.GetFileName(folder);
                    string dest = Common.PathCombine(destFolder, name);
                    if (name != ".svn")
                    {
                        CopyFolder(folder, dest);
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Compresses the selected folder's contents and saves the archive in the specified outputPath
        /// with the extension .epub.
        /// </summary>
        /// <param name="sourceFolder">Folder to compress</param>
        /// <param name="outputPath">Output path and filename (without extension)</param>
        private void Compress(string sourceFolder, string outputPath)
        {
            var mOdt = new ZipFolder();
            string outputPathWithFileName = outputPath + ".epub";

            // add the content to the existing epub.zip file
            string zipFile = Common.PathCombine(sourceFolder, "epub.zip");
            string contentFolder = Common.PathCombine(sourceFolder, "OEBPS");
            string[] files = Directory.GetFiles(contentFolder);
            mOdt.AddToZip(files, zipFile);
            var sb = new StringBuilder();
            sb.Append(sourceFolder);
            sb.Append(Path.DirectorySeparatorChar);
            sb.Append("META-INF");
            sb.Append(Path.DirectorySeparatorChar);
            sb.Append("container.xml");
            var containerFile = new[] { sb.ToString() };
            mOdt.AddToZip(containerFile, zipFile);
            // copy the results to the output directory
            File.Copy(zipFile, outputPathWithFileName, true);
        }

        #endregion

        #region EPUB metadata handlers

        /// <summary>
        /// Generates the manifest and metadata information file used by the .epub reader
        /// (content.opf). For more information, refer to <see cref="http://www.idpf.org/doc_library/epub/OPF_2.0.1_draft.htm#Section2.0"/> 
        /// </summary>
        /// <param name="projInfo">Project information</param>
        /// <param name="contentFolder">Content folder (.../OEBPS)</param>
        /// <param name="bookId">Unique identifier for the book we're generating.</param>
        private void CreateOpf(PublicationInformation projInfo, string contentFolder, Guid bookId)
        {
            XmlWriter opf = XmlWriter.Create(Common.PathCombine(contentFolder, "content.opf"));
            opf.WriteStartDocument();
            // package name
            opf.WriteStartElement("package", "http://www.idpf.org/2007/opf");
            opf.WriteAttributeString("version", "2.0");
            opf.WriteAttributeString("unique-identifier", "BookId");
            // metadata - items defined by the Dublin Core Metadata Initiative:
            // (http://dublincore.org/documents/2004/12/20/dces/)
            opf.WriteStartElement("metadata");
            opf.WriteAttributeString("xmlns", "dc", null, "http://purl.org/dc/elements/1.1/");
            opf.WriteAttributeString("xmlns", "opf", null, "http://www.idpf.org/2007/opf");
            opf.WriteElementString("dc", "title", null,
                                   (Title == "") ? (Common.databaseName + " " + projInfo.ProjectName) : Title);
            opf.WriteStartElement("dc", "creator", null); //<dc:creator opf:role="aut">[author]</dc:creator>
            opf.WriteAttributeString("opf", "role", null, "aut");
            opf.WriteValue((Creator == "") ? Environment.UserName : Creator);
            opf.WriteEndElement();
            opf.WriteElementString("dc", "subject", null, InputType == "dictionary" ? "Reference" : "Religion & Spirituality");
            if (Description.Length > 0)
                opf.WriteElementString("dc", "description", null, Description);
            if (Publisher.Length > 0)
                opf.WriteElementString("dc", "publisher", null, Publisher);
            opf.WriteStartElement("dc", "contributor", null); // authoring program as a "contributor", e.g.:
            opf.WriteAttributeString("opf", "role", null, "bkp");
            // <dc:contributor opf:role="bkp">FieldWorks 7</dc:contributor>
            opf.WriteValue(Common.GetProductName());
            opf.WriteEndElement();
            opf.WriteElementString("dc", "date", null, DateTime.Today.ToString("yyyy-MM-dd"));
            // .epub standard date format (http://www.idpf.org/2007/opf/OPF_2.0_final_spec.html#Section2.2.7)
            opf.WriteElementString("dc", "type", null, "Text"); // 
            if (Format.Length > 0)
                opf.WriteElementString("dc", "format", null, Format);
            if (Source.Length > 0)
                opf.WriteElementString("dc", "source", null, Source);

            if (_langFontDictionary.Count == 0)
            {
                opf.WriteElementString("dc", "language", null, "en");
            }

            foreach (var lang in _langFontDictionary.Keys)
            {
                opf.WriteElementString("dc", "language", null, lang);
            }


            if (Coverage.Length > 0)
                opf.WriteElementString("dc", "coverage", null, Coverage);
            if (Rights.Length > 0)
                opf.WriteElementString("dc", "rights", null, Rights);
            opf.WriteStartElement("dc", "identifier", null); // <dc:identifier id="BookId">[guid]</dc:identifier>
            opf.WriteAttributeString("id", "BookId");
            opf.WriteValue(bookId.ToString());
            opf.WriteEndElement();
            // cover image (optional)
            if (Param.GetMetadataValue(Param.CoverPage).ToLower().Equals("true"))
            {
                opf.WriteStartElement("meta");
                opf.WriteAttributeString("name", "cover");
                opf.WriteAttributeString("content", "cover-image");
                opf.WriteEndElement(); // meta
            }
            opf.WriteEndElement(); // metadata
            // manifest
            opf.WriteStartElement("manifest");
            // (individual "item" elements in the manifest)
            opf.WriteStartElement("item");
            opf.WriteAttributeString("id", "ncx");
            opf.WriteAttributeString("href", "toc.ncx");
            opf.WriteAttributeString("media-type", "application/x-dtbncx+xml");
            opf.WriteEndElement(); // item

            if (EmbedFonts)
            {
                int fontNum = 1;
                foreach (var embeddedFont in _embeddedFonts.Values)
                {
                    if (embeddedFont.Filename == null)
                    {
                        // already written out that this font doesn't exist in the CSS file; just skip it here
                        continue;
                    }
                    opf.WriteStartElement("item"); // item (charis embedded font)
                    opf.WriteAttributeString("id", "epub.embedded.font" + fontNum);
                    opf.WriteAttributeString("href", Path.GetFileName(embeddedFont.Filename));
                    opf.WriteAttributeString("media-type", "font/opentype/");
                    opf.WriteEndElement(); // item
                    fontNum++;
                    if (IncludeFontVariants)
                    {
                        // italic
                        if (embeddedFont.HasItalic && String.Compare(embeddedFont.Filename, embeddedFont.ItalicFilename, StringComparison.Ordinal) != 0)
                        {
                            if (embeddedFont.ItalicFilename != string.Empty)
                            {
                                opf.WriteStartElement("item"); // item (charis embedded font)
                                opf.WriteAttributeString("id", "epub.embedded.font_i_" + fontNum);

                                opf.WriteAttributeString("href", Path.GetFileName(embeddedFont.ItalicFilename));

                                opf.WriteAttributeString("media-type", "font/opentype/");
                                opf.WriteEndElement(); // item
                                fontNum++;
                            }
                        }
                        // bold
                        if (embeddedFont.HasBold && String.Compare(embeddedFont.Filename, embeddedFont.BoldFilename, StringComparison.Ordinal) != 0)
                        {
                            if (embeddedFont.BoldFilename != string.Empty)
                            {
                                opf.WriteStartElement("item"); // item (charis embedded font)
                                opf.WriteAttributeString("id", "epub.embedded.font_b_" + fontNum);
                                opf.WriteAttributeString("href", Path.GetFileName(embeddedFont.BoldFilename));
                                opf.WriteAttributeString("media-type", "font/opentype/");
                                opf.WriteEndElement(); // item
                                fontNum++;
                            }
                        }
                    }
                }
            }
            var listIdRef = new List<string>();
            int counterSet = 1;
            string idRefValue;
            // now add the xhtml files to the manifest
            string[] files = Directory.GetFiles(contentFolder);
            foreach (string file in files)
            {
                // iterate through the file set and add <item> elements for each xhtml file
                string name = Path.GetFileName(file);
                Debug.Assert(name != null);
                string nameNoExt = Path.GetFileNameWithoutExtension(file);

                if (name.EndsWith(".xhtml"))
                {
                    // is this the cover page?
                    if (name.StartsWith(PreExportProcess.CoverPageFilename.Substring(0, 8)))
                    {
                        // yup - write it out and go to the next item
                        opf.WriteStartElement("item");
                        opf.WriteAttributeString("id", "cover");
                        opf.WriteAttributeString("href", name);
                        opf.WriteAttributeString("media-type", "application/xhtml+xml");
                        opf.WriteEndElement(); // item
                        continue;
                    }

                    // if we can, write out the "user friendly" book name in the TOC
                    string fileId = GetBookId(file);

                    if (listIdRef.Contains(fileId))
                    {
                        listIdRef.Add(fileId + counterSet.ToString(CultureInfo.InvariantCulture));
                        idRefValue = fileId + counterSet.ToString(CultureInfo.InvariantCulture);
                        counterSet++;
                    }
                    else
                    {
                        listIdRef.Add(fileId);
                        idRefValue = fileId;
                    }

                    opf.WriteStartElement("item");
                    // the book ID can be wacky (and non-unique) for dictionaries. Just use the filename.
                    var itemId = InputType == "dictionary" ? nameNoExt : idRefValue;
                    opf.WriteAttributeString("id", itemId);
                    opf.WriteAttributeString("href", name);
                    opf.WriteAttributeString("media-type", "application/xhtml+xml");
                    opf.WriteEndElement(); // item
                }
                else if (name.EndsWith(".css"))
                {
                    opf.WriteStartElement("item"); // item (stylesheet)
                    opf.WriteAttributeString("id", "stylesheet");
                    opf.WriteAttributeString("href", name);
                    opf.WriteAttributeString("media-type", "text/css");
                    opf.WriteEndElement(); // item
                }
                else if (name.ToLower().EndsWith(".jpg") || name.ToLower().EndsWith(".jpeg"))
                {
                    opf.WriteStartElement("item"); // item (image)
                    opf.WriteAttributeString("id", "image" + nameNoExt);
                    opf.WriteAttributeString("href", name);
                    if (nameNoExt != null && nameNoExt.Contains("sil-bw-logo"))
                    {
                        opf.WriteAttributeString("media-type", "image/png");    
                    }
                    else
                    {
                        opf.WriteAttributeString("media-type", "image/jpeg");    
                    }
                    
                    opf.WriteEndElement(); // item
                }
                else if (name.ToLower().EndsWith(".gif"))
                {
                    opf.WriteStartElement("item"); // item (image)
                    opf.WriteAttributeString("id", "image" + nameNoExt);
                    opf.WriteAttributeString("href", name);
                    opf.WriteAttributeString("media-type", "image/gif");
                    opf.WriteEndElement(); // item
                }
                else if (name.ToLower().EndsWith(".png"))
                {
                    opf.WriteStartElement("item"); // item (image)
                    opf.WriteAttributeString("id", "image" + nameNoExt);
                    opf.WriteAttributeString("href", name);
                    opf.WriteAttributeString("media-type", "image/png");
                    opf.WriteEndElement(); // item
                }
            }
            opf.WriteEndElement(); // manifest
            // spine
            opf.WriteStartElement("spine");
            opf.WriteAttributeString("toc", "ncx");
            // a couple items for the cover image
            if (Param.GetMetadataValue(Param.CoverPage).ToLower().Equals("true"))
            {
                opf.WriteStartElement("itemref");
                opf.WriteAttributeString("idref", "cover");
                opf.WriteAttributeString("linear", "yes");
                opf.WriteEndElement(); // itemref
            }

            listIdRef = new List<string>();
            counterSet = 1;
            foreach (string file in files)
            {
                // is this the cover page?
                var fileName = Path.GetFileName(file);
                Debug.Assert(fileName != null);
                if (fileName.StartsWith(PreExportProcess.CoverPageFilename.Substring(0, 8)))
                {
                    continue;
                }
                // add an <itemref> for each xhtml file in the set
                if (fileName.EndsWith(".xhtml"))
                {
                    string fileId = GetBookId(file);
                    if (listIdRef.Contains(fileId))
                    {
                        var counter = counterSet.ToString(CultureInfo.InvariantCulture);
                        listIdRef.Add(fileId + counter);
                        idRefValue = fileId + counter;
                        counterSet++;
                    }
                    else
                    {
                        listIdRef.Add(fileId);
                        idRefValue = fileId;
                    }


                    opf.WriteStartElement("itemref"); // item (stylesheet)
                    // the book ID can be wacky (and non-unique) for dictionaries. Just use the filename.
                    var idRef = InputType == "dictionary" ? Path.GetFileNameWithoutExtension(file) : idRefValue;
                    opf.WriteAttributeString("idref", idRef);
                    opf.WriteEndElement(); // itemref
                }
            }
            opf.WriteEndElement(); // spine
            // guide
            opf.WriteStartElement("guide");
            // cover image
            if (Param.GetMetadataValue(Param.CoverPage).Trim().Equals("True"))
            {
                opf.WriteStartElement("reference");
                opf.WriteAttributeString("href", "File0Cvr00000_.xhtml");
                opf.WriteAttributeString("type", "cover");
                opf.WriteAttributeString("title", "Cover");
                opf.WriteEndElement(); // reference
            }
            // first xhtml filename
            opf.WriteStartElement("reference");
            opf.WriteAttributeString("type", "text");
            opf.WriteAttributeString("title", Common.databaseName + " " + projInfo.ProjectName);
            int index = 0;
            while (index < files.Length)
            {
                if (files[index].EndsWith(".xhtml"))
                {
                    break;
                }
                index++;
            }
            if (index == files.Length) index--; // edge case
            opf.WriteAttributeString("href", Path.GetFileName(files[index]));
            opf.WriteEndElement(); // reference
            opf.WriteEndElement(); // guide
            opf.WriteEndElement(); // package
            opf.WriteEndDocument();
            opf.Close();
        }

        /// <summary>
        /// Creates the table of contents file used by .epub readers (toc.ncx).
        /// </summary>
        /// <param name="projInfo">project information</param>
        /// <param name="contentFolder">the content folder (../OEBPS)</param>
        /// <param name="bookId">Unique identifier for the book we're creating</param>
        private void CreateNcx(PublicationInformation projInfo, string contentFolder, Guid bookId)
        {
            // toc.ncx
            string tocFullPath = Common.PathCombine(contentFolder, "toc.ncx");
            XmlWriter ncx = XmlWriter.Create(tocFullPath);
            ncx.WriteStartDocument();
            ncx.WriteStartElement("ncx", "http://www.daisy.org/z3986/2005/ncx/");
            ncx.WriteAttributeString("version", "2005-1");
            ncx.WriteStartElement("head");
            ncx.WriteStartElement("meta");
            ncx.WriteAttributeString("name", "dtb:uid");
            ncx.WriteAttributeString("content", bookId.ToString());
            ncx.WriteEndElement(); // meta
            ncx.WriteStartElement("meta");
            ncx.WriteAttributeString("name", "epub-creator");
            ncx.WriteAttributeString("content", Common.GetProductName());
            ncx.WriteEndElement(); // meta
            ncx.WriteStartElement("meta");
            ncx.WriteAttributeString("name", "dtb:depth");
            ncx.WriteAttributeString("content", "1");
            ncx.WriteEndElement(); // meta
            ncx.WriteStartElement("meta");
            ncx.WriteAttributeString("name", "dtb:totalPageCount");
            ncx.WriteAttributeString("content", "0"); // TODO: (is this possible?)
            ncx.WriteEndElement(); // meta
            ncx.WriteStartElement("meta");
            ncx.WriteAttributeString("name", "dtb:maxPageNumber");
            ncx.WriteAttributeString("content", "0"); // TODO: is this info available?
            ncx.WriteEndElement(); // meta
            ncx.WriteEndElement(); // head
            ncx.WriteStartElement("docTitle");
            ncx.WriteElementString("text", projInfo.ProjectName);
            ncx.WriteEndElement(); // docTitle
            ncx.WriteStartElement("navMap");
            // individual navpoint elements (one for each xhtml)
            string[] files = Directory.GetFiles(contentFolder, "*.xhtml");
            bool isMainOpen = false;
            bool isMainSubOpen = false;
            bool isRevOpen = false;
            bool isRevSubOpen = false;
            bool isScriptureSubOpen = false;
            int index = 1;
            bool skipChapterInfo = TocLevel.StartsWith("1");
            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                Debug.Assert(name != null);
                string bookName = GetBookName(file);
                if (name.IndexOf("File", StringComparison.Ordinal) == 0 && name.IndexOf("TOC", StringComparison.Ordinal) == -1)
                {
                    WriteNavPoint(ncx, index.ToString(CultureInfo.InvariantCulture), bookName, name);
                    index++;
                    // chapters within the books (nested as a subhead)
                    if (!skipChapterInfo)
                    {
                        WriteChapterLinks(file, ref index, ncx);
                    }
                    // end the book's navPoint element
                    ncx.WriteEndElement(); // navPoint
                }
                else
                {
                    if (name.IndexOf("TOC", StringComparison.Ordinal) != -1)
                    {
                        WriteNavPoint(ncx, index.ToString(CultureInfo.InvariantCulture), bookName, name);
                        index++;
                    }
                    if (InputType.ToLower() == "dictionary")
                    {
                        if (name.Contains("PartFile"))
                        {
                            if (!isMainOpen)
                            {
                                isMainOpen = true;
                            }
                            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
                            if (fileNameWithoutExtension != null && (fileNameWithoutExtension.EndsWith("_") ||
                                                                                   fileNameWithoutExtension.EndsWith("_01")))
                            {
                                if (isMainSubOpen)
                                {
                                    ncx.WriteEndElement(); // navPoint
                                }
                                WriteNavPoint(ncx, index.ToString(CultureInfo.InvariantCulture), bookName, name);
                                index++;
                                isMainSubOpen = true;
                            }
                            if (!skipChapterInfo)
                            {
                                WriteChapterLinks(file, ref index, ncx);
                            }
                        }
                        else if (name.Contains("RevIndex"))
                        {
                            if (isMainOpen)
                            {
                                ncx.WriteEndElement(); // navPoint Main
                                isMainSubOpen = false;
                                isMainOpen = false;
                            }
                            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
                            if (fileNameWithoutExtension != null && (fileNameWithoutExtension.EndsWith("_") ||
                                                                                   fileNameWithoutExtension.EndsWith("_01")))
                            {
                                if (isRevSubOpen)
                                {
                                    ncx.WriteEndElement(); // navPoint
                                }
                                if (!isRevOpen)
                                {
                                    isRevOpen = true;
                                }
                                WriteNavPoint(ncx, index.ToString(CultureInfo.InvariantCulture), bookName, name);
                                index++;
                                isRevSubOpen = true;
                            }
                            if (!skipChapterInfo)
                            {
                                WriteChapterLinks(file, ref index, ncx);
                            }
                        }
                    }
                    else
                    {
                        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
                        if (fileNameWithoutExtension != null && (name.IndexOf("TOC", StringComparison.Ordinal) == -1 &&
                                                                               (fileNameWithoutExtension.EndsWith("_") ||
                                                                                fileNameWithoutExtension.EndsWith("_01"))))
                        {
                            if (isScriptureSubOpen)
                            {
                                ncx.WriteEndElement(); // navPoint
                            }
                            bookName = GetBookName(file);
                            ncx.WriteStartElement("navPoint");
                            ncx.WriteAttributeString("id", "dtb:uid");
                            ncx.WriteAttributeString("playOrder", index.ToString(CultureInfo.InvariantCulture));
                            ncx.WriteStartElement("navLabel");
                            ncx.WriteElementString("text", bookName);
                            ncx.WriteEndElement(); // navlabel
                            ncx.WriteStartElement("content");
                            ncx.WriteAttributeString("src", name);
                            ncx.WriteEndElement(); // meta
                            index++;
                            // chapters within the books (nested as a subhead)
                            if (!skipChapterInfo)
                            {
                                WriteChapterLinks(file, ref index, ncx);
                            }
                            isScriptureSubOpen = true;
                        }
                        else if (name.IndexOf("zzReference", StringComparison.Ordinal) == 0)
                        {
                            if (isScriptureSubOpen)
                            {
                                ncx.WriteEndElement(); // navPoint
                            }
                            ncx.WriteStartElement("navPoint");
                            ncx.WriteAttributeString("id", "dtb:uid");
                            ncx.WriteAttributeString("playOrder", index.ToString(CultureInfo.InvariantCulture));
                            ncx.WriteStartElement("navLabel");
                            ncx.WriteElementString("text", "End Notes");
                            ncx.WriteEndElement(); // navlabel
                            ncx.WriteStartElement("content");
                            ncx.WriteAttributeString("src", name);
                            ncx.WriteEndElement(); // meta
                            index++;
                            // chapters within the books (nested as a subhead)
                            if (!skipChapterInfo)
                            {
                                WriteEndNoteLinks(file, ref index, ncx);
                            }
                        }
                        else
                        {
                            if (!skipChapterInfo)
                            {
                                WriteChapterLinks(file, ref index, ncx);
                            }
                        }
                    }
                }

            }
            if (isRevOpen && InputType.ToLower() == "dictionary")
            {
                // end the book's navPoint element
                ncx.WriteEndElement(); // navPoint TOC
            }
            if (isScriptureSubOpen)
            {
                ncx.WriteEndElement(); // navPoint
            }
            ncx.WriteEndElement(); // navPoint TOC
            ncx.WriteEndElement(); // navmap
            ncx.WriteEndDocument();
            ncx.Close();
            FixPlayOrder(tocFullPath);
            if (InputType.ToLower() == "dictionary")
            {
                Common.ApplyXslt(tocFullPath, _addDicTocHeads);
            }
            Common.ApplyXslt(tocFullPath, _fixEpubToc);
            FixPlayOrder(tocFullPath);
        }

        private void AddDtdInXhtml(string contentFolder)
        {
            string[] files = Directory.GetFiles(contentFolder, "*.xhtml");
            foreach (string file in files)
            {
                Common.AddingDTDForLinuxProcess(file);
            }
        }
        private void FixPlayOrder(string tocFullPath)
        {
            // Renumber all PlayOrder attributes in order with no gaps.
            XmlTextReader reader = Common.DeclareXmlTextReader(tocFullPath, true);
            var tocDoc = new XmlDocument();
            tocDoc.Load(reader);
            reader.Close();
            var nodes = tocDoc.SelectNodes("//@playOrder");
            Debug.Assert(nodes != null);
            int n = 1;
            foreach (XmlAttribute node in nodes)
            {
                node.InnerText = n.ToString(CultureInfo.InvariantCulture);
                n += 1;
            }

            if (_isUnixOs)
            {
                nodes = tocDoc.SelectNodes("//@id");
                Debug.Assert(nodes != null);
                n = 1;
                foreach (XmlAttribute node in nodes)
                {
                    node.InnerText = node.InnerText + n.ToString(CultureInfo.InvariantCulture);
                    n += 1;
                }
            }

            var xmlFile = new FileStream(tocFullPath, FileMode.Create);
            XmlWriter writer = XmlWriter.Create(xmlFile);
            tocDoc.Save(writer);
            xmlFile.Close();
        }

        private void WriteNavPoint(XmlWriter ncx, string index, string text, string name)
        {
            ncx.WriteStartElement("navPoint");
            ncx.WriteAttributeString("id", "dtb:uid");
            ncx.WriteAttributeString("playOrder", index);
            ncx.WriteStartElement("navLabel");
            ncx.WriteElementString("text", text);
            ncx.WriteEndElement(); // navlabel
            ncx.WriteStartElement("content");
            ncx.WriteAttributeString("src", name);
            ncx.WriteEndElement(); // meta
        }

        /// <summary>
        /// Writes the chapter links out to the specified XmlWriter (the .ncx file).
        /// </summary>
        /// <returns>List of url strings</returns>
        private void WriteChapterLinks(string xhtmlFileName, ref int playOrder, XmlWriter ncx)
        {
            XmlDocument xmlDocument = Common.DeclareXMLDocument(true);
            var namespaceManager = new XmlNamespaceManager(xmlDocument.NameTable);
            namespaceManager.AddNamespace("xhtml", "http://www.w3.org/1999/xhtml");
            var xmlReaderSettings = new XmlReaderSettings { XmlResolver = null, ProhibitDtd = false }; //Common.DeclareXmlReaderSettings(false);
            var xmlReader = XmlReader.Create(xhtmlFileName, xmlReaderSettings);
            xmlDocument.Load(xmlReader);
            xmlReader.Close();
            bool isSectionHead = false, isChapterNumber = false, isVerseNumber = false;
            string sectionHead = string.Empty, fromChapterNumber = string.Empty, firstVerseNumber = string.Empty, lastVerseNumber = string.Empty;
            var divClass = InputType == "dictionary" ? "entry" : "scrBook";
            var xPath = string.Format("//xhtml:div[@class='{0}']", divClass);
            XmlNodeList nodes = xmlDocument.SelectNodes(xPath, namespaceManager);
            if (nodes != null && nodes.Count > 0)
            {
                var sb = new StringBuilder();
                string name = Path.GetFileName(xhtmlFileName);
                foreach (XmlNode node in nodes)
                {
                    string textString = string.Empty;
                    if (InputType.Equals("dictionary"))
                    {
                        sb.Append(name);
                        sb.Append("#");
                        XmlNode val = null;
                        if (node.Attributes != null && node.Attributes["id"] != null)
                        {
                            val = node.Attributes["id"];
                        }
                        if (val != null)
                            sb.Append(val.Value);

                        // for a dictionary, the headword / headword-minor is the label
                        if (!node.HasChildNodes)
                        {
                            // reset the stringbuilder
                            sb.Length = 0;
                            // This entry doesn't have any information - skip it
                            continue;
                        }
                        const string headwordXPath = ".//xhtml:span[@class='headword']";
                        XmlNode headwordNode = node.SelectSingleNode(headwordXPath, namespaceManager);
                        textString = headwordNode != null ? headwordNode.InnerText : node.FirstChild.InnerText;

                        if (textString.Trim().Length > 0)
                        {
                            // write out the node
                            ncx.WriteStartElement("navPoint");
                            ncx.WriteAttributeString("id", "dtb:uid");
                            ncx.WriteAttributeString("playOrder", playOrder.ToString(CultureInfo.InvariantCulture));
                            ncx.WriteStartElement("navLabel");
                            ncx.WriteElementString("text", textString);
                            ncx.WriteEndElement(); // navlabel
                            ncx.WriteStartElement("content");
                            ncx.WriteAttributeString("src", sb.ToString());
                            ncx.WriteEndElement(); // meta
                            playOrder++;
                        }

                        // If this is a dictionary with TOC level 3, gather the senses for this entry
                        if (InputType.Equals("dictionary") && TocLevel.StartsWith("3"))
                        {
                            // see if there are any senses to add to this entry
                            XmlNodeList childNodes = node.SelectNodes(".//xhtml:span[@class='sense']", namespaceManager);
                            if (childNodes != null)
                            {
                                sb.Length = 0;
                                foreach (XmlNode childNode in childNodes)
                                {
                                    // for a dictionary, the grammatical-info//partofspeech//span is the label
                                    if (!childNode.HasChildNodes)
                                    {
                                        // reset the stringbuilder
                                        sb.Length = 0;
                                        // This entry doesn't have any information - skip it
                                        continue;
                                    }

                                    if (childNode.HasChildNodes && childNode.FirstChild != null && childNode.FirstChild.FirstChild != null)
                                        textString = childNode.FirstChild.FirstChild.InnerText;
                                    sb.Append(name);
                                    sb.Append("#");
                                    if (childNode.Attributes != null && childNode.Attributes["id"] != null)
                                    {
                                        sb.Append(childNode.Attributes["id"].Value);
                                    }
                                    // write out the node
                                    ncx.WriteStartElement("navPoint");
                                    ncx.WriteAttributeString("id", "dtb:uid");
                                    ncx.WriteAttributeString("playOrder", playOrder.ToString(CultureInfo.InvariantCulture));
                                    ncx.WriteStartElement("navLabel");
                                    ncx.WriteElementString("text", textString);
                                    ncx.WriteEndElement(); // navlabel
                                    ncx.WriteStartElement("content");
                                    ncx.WriteAttributeString("src", sb.ToString());
                                    ncx.WriteEndElement(); // meta
                                    ncx.WriteEndElement(); // navPoint
                                    // reset the stringbuilder
                                    sb.Length = 0;
                                    playOrder++;
                                }
                            }
                        }
                        if (textString.Trim().Length > 0)
                        {
                            ncx.WriteEndElement(); // navPoint
                        }
                    }
                    else // Scripture
                    {
                        using (XmlReader reader = XmlReader.Create(new StringReader(node.OuterXml)))
                        {
                            // Parse the file and display each of the nodes.
                            while (reader.Read())
                            {
                                switch (reader.NodeType)
                                {
                                    case XmlNodeType.Element:
                                        string className = reader.GetAttribute("class");
                                        if (className == "Section_Head")
                                        {
                                            if (fromChapterNumber == _currentChapterNumber)
                                            {
                                                textString = textString + "-" + lastVerseNumber + ")";
                                            }
                                            else
                                            {
                                                textString = textString + "-" + _currentChapterNumber + ":" +
                                                               lastVerseNumber + ")";
                                            }
                                            if (textString.Trim().Length >= 4)
                                            {
                                                // write out the node
                                                ncx.WriteStartElement("navPoint");
                                                ncx.WriteAttributeString("id", "dtb:uid");
                                                ncx.WriteAttributeString("playOrder", playOrder.ToString(CultureInfo.InvariantCulture));
                                                ncx.WriteStartElement("navLabel");
                                                ncx.WriteElementString("text", textString);
                                                ncx.WriteEndElement(); // navlabel
                                                ncx.WriteStartElement("content");
                                                ncx.WriteAttributeString("src", sb.ToString());
                                                ncx.WriteEndElement(); // meta
                                                playOrder++;
                                                sb.Length = 0;
                                            }
                                            if (reader.GetAttribute("id") != null)
                                            {
                                                sb.Append(name);
                                                sb.Append("#");
                                                sb.Append(reader.GetAttribute("id"));
                                            }
                                            if (textString.Trim().Length >= 4)
                                            {
                                                ncx.WriteEndElement(); // navPoint
                                            }
                                            textString = string.Empty;
                                            firstVerseNumber = string.Empty;
                                            isSectionHead = true;
                                        }
                                        else if (className == "Chapter_Number")
                                        {
                                            isChapterNumber = true;
                                        }
                                        else if (className != null && className.IndexOf("Verse_Number", StringComparison.Ordinal) == 0)
                                        {
                                            isVerseNumber = true;
                                        }
                                        break;
                                    case XmlNodeType.Text:
                                        if (isSectionHead)
                                        {
                                            sectionHead = reader.Value;
                                            isSectionHead = false;
                                        }
                                        if (isChapterNumber)
                                        {
                                            _currentChapterNumber = reader.Value;
                                            isChapterNumber = false;
                                        }
                                        if (isVerseNumber)
                                        {
                                            if (firstVerseNumber.Trim().Length == 0 && sectionHead.Length > 0)
                                            {
                                                firstVerseNumber = reader.Value;
                                                fromChapterNumber = _currentChapterNumber;
                                                textString = sectionHead + "(" + _currentChapterNumber + ":" + firstVerseNumber;
                                            }
                                            lastVerseNumber = reader.Value;
                                            isVerseNumber = false;
                                        }
                                        break;
                                    case XmlNodeType.XmlDeclaration:
                                    case XmlNodeType.ProcessingInstruction:
                                        break;
                                    case XmlNodeType.Comment:
                                        break;
                                    case XmlNodeType.EndElement:
                                        break;
                                }
                            }
                        }
                    }
                    // reset the stringbuilder
                    sb.Length = 0;
                }
            }
        }


        private void WriteEndNoteLinks(string xhtmlFileName, ref int playOrder, XmlWriter ncx)
        {
            XmlDocument xmlDocument = Common.DeclareXMLDocument(true);
            var namespaceManager = new XmlNamespaceManager(xmlDocument.NameTable);
            namespaceManager.AddNamespace("xhtml", "http://www.w3.org/1999/xhtml");
            var xmlReaderSettings = new XmlReaderSettings { XmlResolver = null, ProhibitDtd = false }; //Common.DeclareXmlReaderSettings(false);
            XmlReader xmlReader = XmlReader.Create(xhtmlFileName, xmlReaderSettings);
            xmlDocument.Load(xmlReader);
            xmlReader.Close();
            bool isanchor = false, isBookName = false, isNoteTargetReference = false, isList = false;
            string anchorValue = string.Empty, bookNameValue = string.Empty;
            XmlNodeList nodes = xmlDocument.SelectNodes("//xhtml:li", namespaceManager);

            if (nodes != null && nodes.Count > 0)
            {
                var sb = new StringBuilder();
                string name = Path.GetFileName(xhtmlFileName);
                foreach (XmlNode node in nodes)
                {
                    string textString = string.Empty;
                    using (XmlReader reader = XmlReader.Create(new StringReader(node.OuterXml)))
                    {
                        // Parse the file and display each of the nodes.
                        while (reader.Read())
                        {
                            switch (reader.NodeType)
                            {
                                case XmlNodeType.Element:
                                    string className = reader.GetAttribute("class");
                                    if (reader.Name == "a")
                                    {
                                        isanchor = true;
                                    }
                                    else if (reader.Name == "li")
                                    {
                                        if (reader.GetAttribute("id") != null)
                                        {
                                            sb.Append(name);
                                            sb.Append("#");
                                            sb.Append(reader.GetAttribute("id"));
                                        }
                                        isList = true;
                                    }
                                    else if (className == "BookName")
                                    {
                                        isBookName = true;
                                    }
                                    else if (className == "Note_Target_Reference")
                                    {
                                        isNoteTargetReference = true;
                                    }
                                    break;
                                case XmlNodeType.Text:
                                    if (isanchor)
                                    {
                                        anchorValue = reader.Value;
                                        isanchor = false;
                                    }
                                    else if (isList)
                                    {
                                        //ListValue = reader.GetAttribute("id");
                                    }
                                    if (isBookName)
                                    {
                                        bookNameValue = reader.Value;
                                        isBookName = false;
                                    }
                                    if (isNoteTargetReference)
                                    {
                                        if (anchorValue.Trim().Length > 0 && bookNameValue.Trim().Length > 0)
                                        {
                                            textString = anchorValue + " " + bookNameValue + " " + reader.Value;
                                        }
                                        if (textString.Trim().Length > 0)
                                        {
                                            // write out the node
                                            ncx.WriteStartElement("navPoint");
                                            ncx.WriteAttributeString("id", "dtb:uid");
                                            ncx.WriteAttributeString("playOrder", playOrder.ToString(CultureInfo.InvariantCulture));
                                            ncx.WriteStartElement("navLabel");
                                            ncx.WriteElementString("text", textString);
                                            ncx.WriteEndElement(); // navlabel
                                            ncx.WriteStartElement("content");
                                            ncx.WriteAttributeString("src", sb.ToString());
                                            ncx.WriteEndElement(); // meta
                                            playOrder++;
                                            sb.Length = 0;
                                        }

                                        if (textString.Trim().Length > 4)
                                        {
                                            ncx.WriteEndElement(); // navPoint
                                        }
                                        isNoteTargetReference = false;
                                    }
                                    break;
                                case XmlNodeType.XmlDeclaration:
                                case XmlNodeType.ProcessingInstruction:
                                    break;
                                case XmlNodeType.Comment:
                                    break;
                                case XmlNodeType.EndElement:
                                    break;
                            }
                        }

                    }
                    // reset the stringbuilder
                    sb.Length = 0;
                }
            }
        }


        protected void InsertChapterLinkBelowBookName(string contentFolder)
        {
            string[] files = Directory.GetFiles(contentFolder, "PartFile*.xhtml");
            var chapterIdList = new List<string>();
            string fileName;
            var xmlDocument = Common.DeclareXMLDocument(true);
            var namespaceManager = new XmlNamespaceManager(xmlDocument.NameTable);
            namespaceManager.AddNamespace("xhtml", "http://www.w3.org/1999/xhtml");
            var xmlReaderSettings = new XmlReaderSettings { XmlResolver = null, ProhibitDtd = false };//Common.DeclareXmlReaderSettings(false);
            foreach (string sourceFile in files)
            {
                if (!File.Exists(sourceFile)) return;
                fileName = Path.GetFileName(sourceFile);
                XmlReader xmlReader = XmlReader.Create(sourceFile, xmlReaderSettings);
                xmlDocument.Load(xmlReader);
                xmlReader.Close();

                if (InputType.Equals("scripture"))
                {
                    XmlNodeList nodes = xmlDocument.SelectNodes(".//xhtml:div[@class='Chapter_Number']", namespaceManager);
                    if (nodes != null)
                        foreach (XmlNode chapterNode in nodes)
                        {
                            if (chapterNode.Attributes != null && (chapterNode.Attributes.Count > 0 && chapterNode.Attributes["id"] != null))
                            {
                                string value = fileName + "#" + chapterNode.Attributes["id"].Value;
                                if (!chapterIdList.Contains(value))
                                    chapterIdList.Add(value);
                            }
                        }
                }
            }
            foreach (string sourceFile in files)
            {
                fileName = Path.GetFileNameWithoutExtension(sourceFile);
                if (fileName != null && (fileName.LastIndexOf("_01", StringComparison.Ordinal) == fileName.Length - 3 || fileName.LastIndexOf("_", StringComparison.Ordinal) == fileName.Length - 1))
                {
                    var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(sourceFile);
                    if (fileNameWithoutExtension != null)
                    {
                        string[] valueList1 = fileNameWithoutExtension.Split('_');
                        if (OnlyOneChapter(chapterIdList, valueList1[0]))
                            continue;
                        XmlReader xmlReader = XmlReader.Create(sourceFile, xmlReaderSettings);
                        xmlDocument.Load(xmlReader);
                        xmlReader.Close();
                        string xPath = ".//xhtml:div[@class='Title_Secondary']";
                        XmlNodeList nodes = xmlDocument.SelectNodes(xPath, namespaceManager);
                        Debug.Assert(nodes != null);

                        if (nodes.Count == 0)
                        {
                            xPath = ".//xhtml:div[@class='Title_Main']";
                            nodes = xmlDocument.SelectNodes(xPath, namespaceManager);
                            Debug.Assert(nodes != null);
                        }

                        if (nodes.Count > 0)
                        {
                            var next = nodes[nodes.Count - 1].NextSibling;
                            if (next != null && next.Attributes != null)
                            {
                                while (next != null && (next.Attributes != null && next.Attributes.GetNamedItem("class").InnerText.ToLower().Contains("title")))
                                    next = next.NextSibling;
                            }
                            foreach (string variable in chapterIdList)
                            {
                                string[] valueList = variable.Split('_');
                                if (valueList[0] != valueList1[0])
                                    continue;

                                Debug.Assert(xmlDocument.DocumentElement != null);
                                XmlNode nodeContent = xmlDocument.CreateElement("a", xmlDocument.DocumentElement.NamespaceURI);
                                Debug.Assert(nodeContent != null && nodeContent.Attributes != null);
                                XmlAttribute attribute = xmlDocument.CreateAttribute("href");
                                attribute.Value = variable;
                                nodeContent.Attributes.Append(attribute);
                                nodeContent.InnerText = GetChapterNumber(variable);
                                Debug.Assert(next != null && next.ParentNode != null);
                                next.ParentNode.InsertBefore(nodeContent, next);
                                Debug.Assert(xmlDocument.DocumentElement != null);
                                XmlNode spaceNode = xmlDocument.CreateElement("span", xmlDocument.DocumentElement.NamespaceURI);
                                spaceNode.InnerText = " ";
                                next.ParentNode.InsertBefore(spaceNode, next);
                            }
                        }
                    }
                    xmlDocument.Save(sourceFile);
                }
            }
        }

        private void RemoveDuplicateBookName(string contentFolder)
        {
            string[] files = Directory.GetFiles(contentFolder, "PartFile*.xhtml");
            XmlDocument xmlDocument = Common.DeclareXMLDocument(true);
            var namespaceManager = new XmlNamespaceManager(xmlDocument.NameTable);
            namespaceManager.AddNamespace("xhtml", "http://www.w3.org/1999/xhtml");
            var xmlReaderSettings = new XmlReaderSettings { XmlResolver = null, ProhibitDtd = false }; //Common.DeclareXmlReaderSettings(false);
            foreach (string sourceFile in files)
            {
                if (!File.Exists(sourceFile)) return;
                XmlReader xmlReader = XmlReader.Create(sourceFile, xmlReaderSettings);
                xmlDocument.Load(xmlReader);
                xmlReader.Close();

                if (InputType.Equals("scripture"))
                {
                    const string xPath = ".//xhtml:div[@class='scrBook']";
                    XmlNodeList nodes = xmlDocument.SelectNodes(xPath, namespaceManager);
                    Debug.Assert(nodes != null);
                    if (nodes.Count > 0)
                    {
                        string titleMainInnerText = string.Empty;
                        const string xPathValue = ".//xhtml:div[@class='Title_Main']";
                        XmlNodeList titleNodes = xmlDocument.SelectNodes(xPathValue, namespaceManager);
                        Debug.Assert(titleNodes != null);
                        if (titleNodes.Count > 0)
                        {
                            titleMainInnerText = titleNodes[0].InnerText;
                        }

                        var nodeInnerText = new StringBuilder();
                        nodeInnerText.Append(nodes[0].InnerXml);
                        nodeInnerText = nodeInnerText.Replace(titleMainInnerText + "</div>" + titleMainInnerText, titleMainInnerText + "</div>");
                        nodes[0].InnerXml = nodeInnerText.ToString();
                    }
                }
                xmlDocument.Save(sourceFile);
            }
        }

        private bool OnlyOneChapter(IEnumerable<string> chapterIdList, string s)
        {
            int count = 0;
            foreach (string s1 in chapterIdList)
            {
                var values = s1.Split('_');
                if (values[0] == s)
                    count += 1;
            }
            return count == 1;
        }

        private string GetChapterNumber(string value)
        {
            string[] valueList = value.Split('_');
            return valueList[valueList.Length - 1].ToLower().Replace("chapter", "");
        }

        private bool GetPageBreakStatus(string cssFileName)
        {
            try
            {
                if (cssFileName.Trim().Length == 0) { return false; }
                Param.LoadSettings();
                XmlDocument xDoc = Common.DeclareXMLDocument(false);
                string path = Param.SettingOutputPath;
                xDoc.Load(path);
                PageBreak = true;
                string xPath = "//stylePick/styles/others/style[@name='" + cssFileName + "']/styleProperty[@name='PageBreak']/@value";
                XmlNode includeImageNode = xDoc.SelectSingleNode(xPath);
                if (includeImageNode != null && includeImageNode.InnerText == "No")
                    PageBreak = false;

            }
            catch { }

            return PageBreak;
        }


        #endregion

        #endregion
    }
}
