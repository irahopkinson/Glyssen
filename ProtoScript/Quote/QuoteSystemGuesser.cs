﻿//#define SHOWTESTINFO

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using ProtoScript.Character;
using ProtoScript.Utilities;
using SIL.ScriptureUtils;

namespace ProtoScript.Quote
{
	public class QuoteSystemGuesser
	{
		private const int kMinSample = 15;
		private const int kMinQuotationDashSample = 15;
		private const double kMinPercent = .70;
		private const double kMinQuotationDashPercent = .25;
		private const double kQuotationDashFailPercent = .10;
		private const double kMaxCompetitorPercent = .6;
		private const int kMaxFollowingVersesToSearchForEndQuote = 7;
		private const int kMaxTimeLimit = 48000000;

		private const int kStartQuoteValue = 2;
		private const int kEndQuoteValue = 2;
		private const int kQuotationDashValue = 3;
		private const int kStartLevel2QuoteValue = 1;
		private const int kEndLevel2QuoteValue = 1;

		private static void IncrementScore(Dictionary<QuoteSystem, int> scores, QuoteSystem quoteSystem, int increment, ref int bestScore)
		{
			scores[quoteSystem] += increment;
			if (scores[quoteSystem] > bestScore)
				bestScore = scores[quoteSystem];
		}

		public static QuoteSystem Guess<T>(ICharacterVerseInfo cvInfo, List<T> bookList, out bool certain, BackgroundWorker worker = null) where T : IScrBook
		{
			certain = false;
			var bookCount = bookList.Count();
			if (bookCount == 0)
			{
				ReportProgressComplete(worker);
				return QuoteSystem.Default;
			}
			var scores = QuoteSystem.UniquelyGuessableSystems.ToDictionary(s => s, s => 0);
			var quotationDashCounts = QuoteSystem.UniquelyGuessableSystems.Where(s => !String.IsNullOrEmpty(s.QuotationDashMarker))
				.ToDictionary(s => s, s => 0);
			var viableSystems = scores.Keys.ToList();
			int totalVersesAnalyzed = 0;
			int totalDialoqueQuoteVersesAnalyzed = 0;
			int maxNonDialogueSamplesPerBook = BCVRef.LastBook * kMinSample / bookCount;
			int booksProcessed = 0;

			int bestScore = 0;
			bool foundEndQuote = false;

			int kVerseValue = Math.Min(kStartQuoteValue + kEndQuoteValue, kQuotationDashValue);

			List<string> followingVerses = new List<string>(kMaxFollowingVersesToSearchForEndQuote);

			var stopwatch = new Stopwatch();
			stopwatch.Start();

			// Start with the New Testament because that's where most of the dialogue quotes are, and it makes guessing A LOT faster!
			foreach (var book in bookList.SkipWhile(b => BCVRef.BookToNumber(b.BookId) < 40).Union(bookList.TakeWhile(b => BCVRef.BookToNumber(b.BookId) < 40)))
			{
				if (worker != null)
					worker.ReportProgress(MathUtilities.Percent(++booksProcessed, bookCount));

				int versesAnalyzedForCurrentBook = 0;
				int prevQuoteChapter = -1;
				int prevQuoteVerse = -1;

				foreach (var quote in cvInfo.GetAllQuoteInfo(book.BookId).Where(q => q.IsExpected))
				{
					if (versesAnalyzedForCurrentBook > maxNonDialogueSamplesPerBook && !quote.IsDialogue)
						continue;

					if (quote.Chapter == prevQuoteChapter && (quote.Verse == prevQuoteVerse || quote.Verse == prevQuoteVerse + 1))
					{
						prevQuoteVerse = quote.Verse;
						continue;
					}
					var text = book.GetVerseText(quote.Chapter, quote.Verse);
					followingVerses.Clear();
					int maxFollowingVersesToSearch = kMaxFollowingVersesToSearchForEndQuote;
#if SHOWTESTINFO
					Debug.WriteLine("Evaluating {0} {1}:{2} - contents: {3}", book.BookId, quote.Chapter, quote.Verse, text);
#endif
					foreach (var quoteSystem in viableSystems)
					{
						int ichStartQuote = text.IndexOf(quoteSystem.FirstLevel.Open, StringComparison.Ordinal);
						int i2 = -1;

						if (quote.IsDialogue && !string.IsNullOrEmpty(quoteSystem.QuotationDashMarker))
						{
							int i = text.IndexOf(quoteSystem.QuotationDashMarker, StringComparison.Ordinal);
							if (i >= 0 && (ichStartQuote < 0 || i < ichStartQuote))
							{
								// Found a dialogue quote marker earlier in the text.
								IncrementScore(scores, quoteSystem, kQuotationDashValue, ref bestScore);
								quotationDashCounts[quoteSystem]++;
								continue;
							}
						}
						if (ichStartQuote >= 0 && ichStartQuote < text.Length - 2)
						{
							IncrementScore(scores, quoteSystem, kStartQuoteValue, ref bestScore);

							//if (quoteSystem.NormalLevels.Count() > 1)
							//{
							//	i2 = text.IndexOf(quoteSystem.NormalLevels[1].Open, ichStartQuote + 1, StringComparison.Ordinal);
							//	if (i2 > ichStartQuote)
							//	{
							//		IncrementScore(scores, quoteSystem, kStartLevel2QuoteValue, ref bestScore);
							//		if (i2 < text.Length - 2 && text.IndexOf(quoteSystem.NormalLevels[1].Close, i2 + 1, StringComparison.Ordinal) > i2)
							//			IncrementScore(scores, quoteSystem, kEndLevel2QuoteValue, ref bestScore);
							//	}
							//}

							if (text.IndexOf(quoteSystem.FirstLevel.Close, ichStartQuote + 1, StringComparison.Ordinal) > ichStartQuote)
							{
								foundEndQuote = true;
								IncrementScore(scores, quoteSystem, kEndQuoteValue, ref bestScore);
							}
							else
							{
								for (int i = 1; i <= maxFollowingVersesToSearch; i++)
								{
									if (!cvInfo.GetCharacters(book.BookId, quote.Chapter, quote.Verse + i).Any())
										break;
									string followingText;
									if (followingVerses.Count >= i )
										followingText = followingVerses[i - 1];
									else
									{
										followingText = book.GetVerseText(quote.Chapter, quote.Verse + i);
										followingVerses.Add(followingText);
									}
									//if (i2 >= 0 && followingText.IndexOf(quoteSystem.NormalLevels[1].Close, StringComparison.Ordinal) >= 0)
									//{
									//	IncrementScore(scores, quoteSystem, kEndLevel2QuoteValue, ref bestScore);
									//}
									if (followingText.IndexOf(quoteSystem.FirstLevel.Close, StringComparison.Ordinal) > 0)
									{
										foundEndQuote = true;
										IncrementScore(scores, quoteSystem, kEndQuoteValue, ref bestScore);
										break;
									}
								}
							}
							maxFollowingVersesToSearch = followingVerses.Count;
						}
					}
					totalVersesAnalyzed++;
					if (quote.IsDialogue)
						totalDialoqueQuoteVersesAnalyzed++;
					versesAnalyzedForCurrentBook++;

					if (totalVersesAnalyzed >= kMinSample && foundEndQuote &&
						(totalDialoqueQuoteVersesAnalyzed >= kMinQuotationDashSample ||
						viableSystems.TrueForAll(s => String.IsNullOrEmpty(s.QuotationDashMarker))))
					{
						var minViabilityScore = Math.Max(totalVersesAnalyzed * kVerseValue * kMinPercent,
							bestScore * kMaxCompetitorPercent);
						var competitors = viableSystems.Where(system => scores[system] > minViabilityScore).ToList();

						if (competitors.Any())
						{
#if SHOWTESTINFO
							Debug.WriteLine("STATISTICS:");
							foreach (var system in competitors)
							{
								Debug.WriteLine(system.Name + "(" + system + ")\tScore: " + scores[system]);
								if (!String.IsNullOrEmpty(system.QuotationDashMarker))
								{
									Debug.WriteLine("\tPercentage matches of total Dialogue quotes analyzed: " + (100.0 * quotationDashCounts[system]) / totalDialoqueQuoteVersesAnalyzed);
								}
							}
#endif

							if (competitors.Count == 1)
							{
								certain = true;
								ReportProgressComplete(worker);
								return competitors[0];
							}

							viableSystems = viableSystems.Where(competitors.Contains).ToList();
							if (competitors.TrueForAll(c => c.FirstLevel.Open == competitors[0].FirstLevel.Open &&
								c.FirstLevel.Close == competitors[0].FirstLevel.Close))
							{
								var contendersWithQDash = competitors.Where(c => !String.IsNullOrEmpty(c.QuotationDashMarker)).ToList();
								if (contendersWithQDash.TrueForAll(
									c => quotationDashCounts[c] < kQuotationDashFailPercent * totalDialoqueQuoteVersesAnalyzed))
								{
									competitors = competitors.Where(c => String.IsNullOrEmpty(c.QuotationDashMarker)).ToList();

									// We're probably (unless we reset this to false below) down to either a single contender (in
									// which case we can be pretty certain) or two contenders, in which case we can safely use the
									// one with multiple levels filled in (since there will be no harm done even if the data only
									// has 1st-level quotes).
									certain = true;	
								}
								else
								{
									competitors = contendersWithQDash.Where(c => scores[c] == bestScore &&
										quotationDashCounts[c] > kMinQuotationDashPercent * totalDialoqueQuoteVersesAnalyzed).ToList();
								}

								if (competitors.Any())
								{
									// If there are multiple systems with 2nd and 3rd levels specified, discard those options since
									// we didn't find anything in the data to help us choose among the options.
									if (competitors.Count(qs => qs.NormalLevels.Count > 1) > 1)
									{
										competitors = competitors.Where(qs => qs.NormalLevels.Count() == 1).ToList();
										certain = false;
									}

									if (competitors.Any())
									{
										ReportProgressComplete(worker);

										if (competitors.Count == 1)
											return competitors[0];
										return competitors.FirstOrDefault(qs => qs.NormalLevels.Count() > 1) ?? competitors.First();
									}
								}
							}
							// Still have multiple systems in contention with different first-level start & end markers;
							// we haven't seen enough evidence to pick a clear winner.
						}
					}

					if (stopwatch.ElapsedMilliseconds > kMaxTimeLimit)
					{
#if SHOWTESTINFO
						Debug.WriteLine("Time-out guessing quote system.");
#endif
						ReportProgressComplete(worker);
						return BestGuess(viableSystems, scores, bestScore, foundEndQuote);
					}

					prevQuoteChapter = quote.Chapter;
					prevQuoteVerse = quote.Verse;
				}
			}
			ReportProgressComplete(worker);
			return BestGuess(viableSystems, scores, bestScore, foundEndQuote);
		}

		private static QuoteSystem BestGuess(IEnumerable<QuoteSystem> viableSystems, Dictionary<QuoteSystem, int> scores, int bestScore, bool foundEndQuote)
		{
			var bestSystems = viableSystems.Where(s => scores[s] == bestScore).ToList();

			if (bestSystems.Count == 0 || !foundEndQuote)
			{
#if SHOWTESTINFO
				if (bestSystems.Count == 0)
					Debug.WriteLine("No best system found. Using default.");
				if (!foundEndQuote)
					Debug.WriteLine("No end-quote match found for any system. Using default.");
#endif
				return QuoteSystem.Default;
			}

			if (bestSystems.Count == 1)
				return bestSystems[0];

			if (bestSystems.Count(qs => qs.NormalLevels.Count > 1) == 1)
				return bestSystems.Single(qs => qs.NormalLevels.Count > 1);

			if (bestSystems.TrueForAll(c => c.FirstLevel.Open == bestSystems[0].FirstLevel.Open &&
				c.FirstLevel.Close == bestSystems[0].FirstLevel.Close))
			{
				var systemWithFirstLevelOnly = viableSystems.FirstOrDefault(s => s.FirstLevel.Equals(bestSystems[0].FirstLevel));
				if (systemWithFirstLevelOnly != null)
					return systemWithFirstLevelOnly;
			}
			return bestSystems.First();
		}

		//private static QuoteSystem SystemWithAllLevels(QuoteSystem system, Dictionary<QuoteSystem, int> scores)
		//{
		//	// We don't want to modify one of the existing systems
		//	var newSystem = new QuoteSystem(system);

		//	QuotationMark level1 = newSystem.FirstLevel;
		//	QuotationMark level2 = null;
		//	var level2Possibilities = QuoteUtils.GetLevel2Possibilities(level1);
			
		//	if (level2Possibilities == null)
		//		return newSystem;
		//	int count = level2Possibilities.Count();
		//	if (count == 0)
		//		return newSystem;

		//	if (count > 1)
		//	{
		//		QuotationMark possibilityWithHighestScore = null;
		//		int highestScore = 0;
		//		foreach (var qs in scores)
		//		{
		//			foreach (var possibility in level2Possibilities)
		//			{
		//				if (qs.Key.FirstLevel.Open == possibility.Open && qs.Key.FirstLevel.Close == possibility.Close && qs.Value > highestScore)
		//				{
		//					highestScore = qs.Value;
		//					possibilityWithHighestScore = qs.Key.FirstLevel;
		//				}
		//			}
		//		}
		//		level2 = possibilityWithHighestScore;
		//	}
		//	if (level2 == null)
		//		level2 = QuoteUtils.GetLevel2Default(level1);

		//	newSystem.AllLevels.Add(new QuotationMark(level2.Open, level2.Close, QuoteUtils.GenerateLevel2ContinuerByConcatenation(newSystem, level2.Continue), 2, QuotationMarkingSystemType.Normal));
		//	newSystem.AllLevels.Add(QuoteUtils.GenerateLevel3(newSystem, true));
		//	return newSystem;
		//}

		private static void ReportProgressComplete(BackgroundWorker worker)
		{
			if (worker != null)
				worker.ReportProgress(100);
		}
	}
}
