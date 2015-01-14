﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Palaso.Xml;
using ProtoScript.Properties;

namespace ProtoScript.Bundle
{
	[XmlRoot("DBLMetadata")]
	public class DblMetadataBase
	{
		[XmlAttribute] public string id;
		[XmlAttribute] public string type;
		[XmlAttribute] public string typeVersion;

		public bool IsTextReleaseBundle { get { return type == "text"; } }
	}

	[XmlRoot("DBLMetadata")]
	public class DblMetadata : DblMetadataBase
	{
		/// <summary>This is not part of the original DBL metadata. We add this when we parse the USX to create
		/// a script. If significant changes to the parser are made and the parser version in the program does
		/// not match the stored parser version, then we know to re-parse the original USX data.</summary>
		[XmlAttribute("usxparserversion")]
		public string PgUsxParserVersion;

		/// <summary>This is not part of the original DBL metadata. We add this when we parse the USX to create
		/// a script. This tells us the original (local) path of the DBL file used to create this project.</summary>
		[XmlAttribute("origdblpath")]
		public string OriginalPathOfDblFile;

		/// <summary>This is not part of the original DBL metadata. We add this when we parse an SFM file to create
		/// a script. This tells us the original (local) path of the SFM file used to create this project.</summary>
		[XmlAttribute("origsfmfile")]
		public string OriginalPathOfSfmFile;

		/// <summary>This is not part of the original DBL metadata. We add this when we parse a directory of SFM files to create
		/// a script. This tells us the original (local) path of the directory used to create this project.</summary>
		[XmlAttribute("origsfmdir")]
		public string OriginalPathOfSfmDirectory;

		/// <summary>
		/// This is not part of the original DBL metadata. 
		/// We use this to know if character assignments should be reprocessed.
		/// </summary>
		[XmlAttribute("controlfileversion")]
		public int ControlFileVersion;

		/// <summary>
		/// This is not part of the original DBL metadata.
		/// </summary>
		public QuoteSystem QuoteSystem;
		
		/// <summary>
		/// This is not part of the original DBL metadata but rather is pulled in from the stylesheet.
		/// </summary>
		[XmlElement("fontFamily")]
		public string FontFamily;

		/// <summary>
		/// This is not part of the original DBL metadata but rather is pulled in from the stylesheet.
		/// </summary>
		[XmlElement("fontSizeInPoints")]
		public int FontSizeInPoints {
			get { return m_fontSizeInPoints == 0 ? Settings.Default.DefaultFontSize : m_fontSizeInPoints; }
			set { m_fontSizeInPoints = value; }
		}
		private int m_fontSizeInPoints;

		public DblMetadataIdentification identification;
		public DblMetadataLanguage language;
		public DblMetadataPromotion promotion;
		public DblMetadataArchiveStatus archiveStatus;
		[XmlArray("bookNames")]
		[XmlArrayItem("book")]
		public List<Book> AvailableBooks { get; set; }

		public string GetAsXml()
		{
			return XmlSerializationHelper.SerializeToString(this);
		}

		public static DblMetadata Load(string projectFilePath, out Exception exception)
		{
			return XmlSerializationHelper.DeserializeFromFile<DblMetadata>(projectFilePath, out exception);
		}

		public override string ToString()
		{
			string format = "{0} - {1}";
			string languagePart;
			if (string.IsNullOrEmpty(language.name))
			{
				if (language.iso == "sample")
				{
					languagePart = string.Empty;
					format = "{1}";
				}
				else
					languagePart = language.iso;
			}
			else
				languagePart = string.Format("{0} ({1})", language.name, language.iso);

			string identificationPart;
			if (identification == null)
				identificationPart = id;
			else
			{
				if (identification.nameLocal == identification.name)
					identificationPart = identification.nameLocal;
				else
					identificationPart = String.Format("{0} ({1})", identification.nameLocal, identification.name);
			}

			return String.Format(format, languagePart, identificationPart);
		}
	}

	public class DblMetadataIdentification
	{
		public string name;
		public string nameLocal;

		[XmlElement("systemId")]
		public HashSet<DblMetadataSystemId> systemIds;
	}

	public class DblMetadataLanguage
	{
		public string iso;
		public string name;
		public string ldml;
		public string rod;
		public string script;
		[DefaultValue("LTR")]
		public string scriptDirection;
		public string numerals;

		public override string ToString()
		{
			return iso;
		}
	}

	public class DblMetadataPromotion
	{
		[XmlElement("promoVersionInfo")]
		public DblMetadataXhtmlContentNode promoVersionInfo;

		[XmlElement("promoEmail")]
		public DblMetadataXhtmlContentNode promoEmail;
	}

	public class DblMetadataXhtmlContentNode
	{
		private string m_value;

		public DblMetadataXhtmlContentNode()
		{
			contentType = "xhtml";
		}

		[XmlAttribute]
		public string contentType;

		[XmlAnyElement]
		public XmlElement[] InternalNodes { get; set; }

		[XmlIgnore]
		public string value
		{
			get
			{
				if (m_value == null)
				{
					var sb = new StringBuilder();
					foreach (var node in InternalNodes)
						sb.Append(node.OuterXml);
					m_value = sb.ToString();
				}
				return m_value;
			}
			set
			{
				m_value = value;
				var doc = new XmlDocument();
				doc.LoadXml(value);
				InternalNodes = new[] { doc.DocumentElement };
			}
		}
	}

	public class DblMetadataSystemId
	{
		[XmlAttribute]
		public string type;

		[XmlText]
		public string value;
	}

	public class DblMetadataArchiveStatus
	{
		public string dateArchived;
		public string dateUpdated;
	}

	public class Book
	{
		public Book()
		{
			IncludeInScript = true;
		}

		[XmlAttribute("code")]
		public string Code { get; set; }

		[XmlAttribute("include")]
		[DefaultValue(true)]
		public bool IncludeInScript { get; set; }

		[XmlElement("long")]
		public string LongName { get; set; }

		[XmlElement("short")]
		public string ShortName { get; set; }

		[XmlElement("abbr")]
		public string Abbreviation { get; set; }
	}
}
