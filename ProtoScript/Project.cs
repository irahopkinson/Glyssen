﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using L10NSharp;
using Palaso.Reporting;
using Palaso.Xml;
using Paratext;
using ProtoScript.Bundle;
using ProtoScript.Properties;
using Canon = ProtoScript.Bundle.Canon;

namespace ProtoScript
{
	internal class Project
	{
		public const string kProjectFileExtension = ".pgproj";
		private readonly DblMetadata m_metadata;
		private readonly List<BookScript> m_books = new List<BookScript>();

		public Project(DblMetadata metadata)
		{
			m_metadata = metadata;
		}

		public string Id
		{
			get { return m_metadata.id; }
		}

		public string Language
		{
			get { return m_metadata.language.ToString(); }
		}

		public QuoteSystem QuoteSystem
		{
			get { return m_metadata.QuoteSystem ?? QuoteSystem.Default; }
			set
			{
				bool quoteSystemChanged = m_metadata.QuoteSystem != value;
				m_metadata.QuoteSystem = value;
				if (quoteSystemChanged)
					HandleQuoteSystemChanged();
			}
		}

		public static Project Load(string projectFilePath)
		{
			Project project;
			Exception exception;
			var metadata = DblMetadata.Load(projectFilePath, out exception);
			if (exception != null)
			{
				ErrorReport.ReportNonFatalExceptionWithMessage(exception,
					LocalizationManager.GetString("File.ProjectMetadataInvalid", "Project could not be loaded: {0}"), projectFilePath);
				return null;
			}
			if (metadata.PgUsxParserVersion != Settings.Default.PgUsxParserVersion &&
				File.Exists(metadata.OriginalPathOfDblFile))
			{
				// ENHANCE: For now, just create a new bundle and re-parse
				var bundle = new Bundle.Bundle(metadata.OriginalPathOfDblFile);
				// See if we already have a project for this bundle and open it instead.
				project = new Project(bundle.Metadata);
				project.PopulateAndParseBooks(bundle);
				return project;
			}
			project = new Project(metadata);
			var projectDir = Path.GetDirectoryName(projectFilePath);
			Debug.Assert(projectDir != null);
			foreach (var file in Directory.GetFiles(projectDir, "???.xml"))
			{
				project.m_books.Add(XmlSerializationHelper.DeserializeFromFile<BookScript>(file));
			}
			if (metadata.ControlFileVersion != CharacterVerse.ControlFileVersion)
			{
				new CharacterAssigner().AssignAll(project.m_books);
				metadata.ControlFileVersion = CharacterVerse.ControlFileVersion;
			}
			return project;
		}

		public void PopulateAndParseBooks(Bundle.Bundle bundle)
		{
			Canon canon;
			if (bundle.TryGetCanon(1, out canon))
			{
				UsxDocument book;
				if (canon.TryGetBook("MRK", out book))
				{
					ParseAndAddBooks(new [] { book }, bundle.Stylesheet);
				}
			}
		}

		public void ParseAndAddBooks(IEnumerable<UsxDocument> books, IStylesheet stylesheet)
		{
			foreach (var book in books)
			{
				var bookId = book.BookId;
				m_books.Add(new BookScript(bookId, new UsxParser(bookId, stylesheet, book.GetChaptersAndParas()).Parse()));
			}

			if (m_metadata.QuoteSystem == null)
			{
				m_metadata.QuoteSystem = QuoteSystemGuesser.Guess(m_books);
			}

			foreach (var bookScript in m_books)
			{
				bookScript.Blocks = new QuoteParser(bookScript.BookId, bookScript.Blocks, QuoteSystem).Parse().ToList();
			}
		}

		public static string GetProjectFilePath(string basePath, string langId, string bundleId)
		{
			return Path.Combine(basePath, langId, bundleId, langId + kProjectFileExtension);
		}

		public void Save(string path)
		{
			var projectPath = GetProjectFilePath(path, m_metadata.language.ToString(), m_metadata.id);
			Directory.CreateDirectory(Path.GetDirectoryName(projectPath));
			Exception error;
			XmlSerializationHelper.SerializeToFile(projectPath, m_metadata, out error);
			if (error != null)
			{
				MessageBox.Show(error.Message);
				return;
			}
			Settings.Default.CurrentProject = projectPath;
			var projectFolder = Path.GetDirectoryName(projectPath);
			foreach (var book in m_books)
			{
				var filePath = Path.ChangeExtension(Path.Combine(projectFolder, book.BookId), "xml");
				XmlSerializationHelper.SerializeToFile(filePath, book, out error);
				if (error != null)
					MessageBox.Show(error.Message);
			}
		}

		public void ExportTabDelimited(string fileName)
		{
			int blockNumber = 1;
			using (StreamWriter stream = new StreamWriter(fileName, false, Encoding.UTF8))
			{
				foreach (var book in m_books)
				{
					foreach (var block in book.Blocks)
					{
						stream.WriteLine((blockNumber++) + "\t" + block.GetAsTabDelimited(book.BookId));
					}
				}
			}
		}

		private void HandleQuoteSystemChanged()
		{
			if (File.Exists(m_metadata.OriginalPathOfDblFile))
			{
				var bundle = new Bundle.Bundle(m_metadata.OriginalPathOfDblFile);
				PopulateAndParseBooks(bundle);
			}
			else
			{
				//TODO
				throw new ApplicationException();
			}
		}
	}
}
