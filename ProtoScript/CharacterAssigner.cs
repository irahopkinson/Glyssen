﻿using System.Collections.Generic;

namespace ProtoScript
{
	public class CharacterAssigner
	{
		public void AssignAll(ICollection<BookScript> bookScripts, bool overwriteUserConfirmed = false)
		{
			foreach (BookScript bookScript in bookScripts)
				Assign(bookScript, overwriteUserConfirmed);
		}

		public void Assign(BookScript bookScript, bool overwriteUserConfirmed = false)
		{
			foreach (Block block in bookScript.ScriptBlocks)
			{
				if (block.UserConfirmed && !overwriteUserConfirmed)
					continue;
				block.CharacterId = CharacterVerse.GetCharacter(bookScript.BookId, block.ChapterNumber, block.InitialVerseNumber);
			}
		}
	}
}