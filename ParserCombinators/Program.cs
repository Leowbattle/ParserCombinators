using System;
using System.Collections.Generic;
using System.Linq;

namespace ParserCombinators
{
	// https://bodil.lol/parser-combinators/

	class Program
	{
		static void Main(string[] args)
		{
			string input = @"<aaa><a><bbb attribute1=""hi""><c/><d/><e/></bbb></a></aaa>";

			var (_, e) = ParseElement(input);
		}

		public static (string input, string id) Identifier(string input)
		{
			int length = 0;

			if (!char.IsLetter(input[0]))
			{
				throw new Exception();
			}

			length += 1;

			foreach (char c in input.Skip(1))
			{
				if (!(char.IsLetterOrDigit(c) || c == '-'))
				{
					break;
				}
				length += 1;
			}

			return (input.Substring(length), input.Substring(0, length));
		}

		public static (string input, (string k, string v)) AttributePair(string input)
		{
			return Parsers.Pair(
				Identifier,
				Parsers.Right(
					Parsers.MatchChar('='),
					Parsers.QuotedString))(input);
		}

		public static (string input, IReadOnlyDictionary<string, string>) Attributes(string input)
		{
			return Parsers.Map(
				Parsers.ZeroOrMore(
					Parsers.Right(
						Parsers.Whitespace0,
						Parsers.Left(
							AttributePair,
							Parsers.Whitespace0))),
				kvPairs => kvPairs.ToDictionary(kvp => kvp.k, kvp => kvp.v))(input);
		}

		public static (string input, (string id, IReadOnlyDictionary<string, string>)) ElementStart(string input)
		{
			return Parsers.Right(
				Parsers.MatchChar('<'),
				Parsers.Pair(
					Identifier,
					Attributes))(input);
		}

		public static (string input, Element) SingleElement(string input)
		{
			return Parsers.Map(
				Parsers.Left(
					ElementStart,
					Parsers.MatchLiteral("/>")),
				e => new Element
				{
					Name = e.id,
					Attributes = e.Item2
				})(input);
		}

		public static (string input, Element) OpenElement(string input)
		{
			return Parsers.Map(
				Parsers.Left(
					ElementStart,
					Parsers.MatchLiteral(">")),
				e => new Element
				{
					Name = e.id,
					Attributes = e.Item2
				})(input);
		}

		public static Func<string, (string input, string)> CloseElement(string expected)
		{
			return (string input) =>
			{
				return Parsers.Pred(Parsers.Right(
					Parsers.MatchLiteral("</"),
					Parsers.Left(
						Identifier,
						Parsers.MatchChar('>'))),
					id => id == expected)(input);
			};
		}

		public static (string input, Element) ParentElement(string input)
		{
			return Parsers.AndThen(
				OpenElement,
				e => Parsers.Map(
					Parsers.Left(
						Parsers.ZeroOrMore(ParseElement),
						CloseElement(e.Name)),
					es => new Element
					{
						Name = e.Name,
						Attributes = e.Attributes,
						Children = es
					}))(input);
		}

		public static (string input, Element) ParseElement(string input)
		{
			return Parsers.Either(
				SingleElement,
				ParentElement)(input);
		}
	}

	public class Element
	{
		public string Name { get; init; }
		public IReadOnlyDictionary<string, string> Attributes { get; init; }
		public IReadOnlyList<Element> Children { get; init; }
	}

	public static class Parsers
	{
		public static Func<string, (string input, object)> MatchLiteral(string expected)
		{
			return (string input) =>
			{
				if (input.StartsWith(expected))
				{
					return (input.Substring(expected.Length), null);
				}
				else
				{
					throw new Exception();
				}
			};
		}

		public static Func<string, (string input, (T1, T2))> Pair<T1, T2>(Func<string, (string input, T1 t1)> p1, Func<string, (string input, T2 t2)> p2)
		{
			return (string input) =>
			{
				var (input2, t1) = p1(input);
				var (input3, t2) = p2(input2);

				return (input3, (t1, t2));
			};
		}

		public static Func<string, (string input, Tout)> Map<Tin, Tout>(Func<string, (string input, Tin)> p, Func<Tin, Tout> func)
		{
			return (string input) =>
			{
				var (input2, tin) = p(input);
				return (input2, func(tin));
			};
		}

		public static Func<string, (string input, T1)> Left<T1, T2>(Func<string, (string input, T1)> p1, Func<string, (string input, T2)> p2)
		{
			return (string input) =>
			{
				var (input2, t1) = p1(input);
				var (input3, _) = p2(input2);
				return (input3, t1);
			};
		}

		public static Func<string, (string input, T2)> Right<T1, T2>(Func<string, (string input, T1)> p1, Func<string, (string input, T2)> p2)
		{
			return (string input) =>
			{
				var (input2, _) = p1(input);
				var (input3, t2) = p2(input2);
				return (input3, t2);
			};
		}

		public static Func<string, (string input, IReadOnlyList<T>)> ZeroOrMore<T>(Func<string, (string input, T)> p)
		{
			return (string input) =>
			{
				List<T> items = new List<T>();

				string input2 = input;

				// Oh no! Exceptions for control flow!
				try
				{
					while (true)
					{
						var (input3, item) = p(input2);
						input2 = input3;
						items.Add(item);
					}
				}
				catch { }

				return (input2, items);
			};
		}

		public static Func<string, (string input, IReadOnlyList<T>)> OneOrMore<T>(Func<string, (string input, T)> p)
		{
			return (string input) =>
			{
				var (input2, items) = ZeroOrMore(p)(input);
				if (items.Count < 1)
				{
					throw new Exception();
				}
				return (input2, items);
			};
		}

		public static (string input, char) AnyChar(string input)
		{
			if (input.Length > 0)
			{
				return (input.Substring(1), input[0]);
			}
			throw new Exception();
		}

		public static Func<string, (string input, char)> MatchChar(char c)
		{
			return (string input) =>
			{
				if (input[0] == c)
				{
					return (input.Substring(1), c);
				}
				throw new Exception();
			};
		}

		public static Func<string, (string input, T)> Pred<T>(Func<string, (string input, T)> p, Predicate<T> pred)
		{
			return (string input) =>
			{
				var (input2, t) = p(input);
				if (pred(t))
				{
					return (input2, t);
				}
				throw new Exception();
			};
		}

		public static Func<string, (string input, T)> Either<T>(Func<string, (string input, T)> p1, Func<string, (string input, T)> p2)
		{
			return (string input) =>
			{
				try
				{
					return p1(input);
				}
				catch
				{
					return p2(input);
				}
			};
		}

		public static Func<string, (string input, T2)> AndThen<T1, T2>(Func<string, (string input, T1)> p, Func<T1, Func<string, (string input, T2)>> f)
		{
			return (string input) =>
			{
				var (input2, t1) = p(input);
				return f(t1)(input2);
			};
		}

		public static (string input, char) WhitespaceChar(string input)
		{
			return Pred(AnyChar, c => char.IsWhiteSpace(c))(input);
		}

		public static (string input, IReadOnlyList<char>) Whitespace0(string input)
		{
			return ZeroOrMore(WhitespaceChar)(input);
		}

		public static (string input, IReadOnlyList<char>) Whitespace1(string input)
		{
			return OneOrMore(WhitespaceChar)(input);
		}

		public static (string input, string) QuotedString(string input)
		{
			return Map(
				Right(
					MatchChar('"'),
					Left(
						ZeroOrMore(Pred(AnyChar, c => c != '"')),
						MatchChar('"'))),
				chars => new string(chars.ToArray()))(input);
		}
	}
}
