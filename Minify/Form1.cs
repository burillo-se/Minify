using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Minify
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        const string quoteChars = "\'\"";
        const string tokenChars = "+-*/=<>;:{}[](),.?\\%|&";
        const string header_region = "SEHEADER";
        const string footer_region = "SEFOOTER";

        bool isQuote(char c)
        {
            return quoteChars.Contains(c);
        }

        bool isToken(char c)
        {
            return tokenChars.Contains(c);
        }

        string minifyLine(string line)
        {

            StringBuilder sb = new StringBuilder();

            for (int cur_idx = 0; cur_idx < line.Length;)
            {
                char c = line[cur_idx];
                // find start of next word
                if (char.IsWhiteSpace(c)) {
                    cur_idx++;
                    continue;
                }
                // we're not a whitespace, that means we're either a
                // word, a token or a quote

                // if we're a quote, copy whatever that follows verbatim
                if (isQuote(c))
                {
                    // find closing quote
                    int end = line.Length;
                    for (int i = cur_idx + 1; i < line.Length; i++)
                    {
                        // make sure we close the same quotes, and that we don't close
                        // quotes inside a string
                        if (line[i] == c && line[i -1] != '\\')
                        {
                            end = i;
                            break;
                        }
                    }
                    sb.Append(line.Substring(cur_idx, end - cur_idx));
                    cur_idx += end - cur_idx;
                    continue;
                }
                if (isToken(c))
                {
                    cur_idx++;
                    sb.Append(c);
                    continue;
                }
                // we're a word, so make sure we copy the whole word and a following space
                // IF what follows is another word
                sb.Append(c);
                cur_idx++;

                if (cur_idx + 1 < line.Length &&
                    char.IsWhiteSpace(line[cur_idx]) &&
                    char.IsLetterOrDigit(line[cur_idx + 1]))
                {
                    sb.Append(line.Substring(cur_idx, 1));
                    cur_idx++;
                }
            }

            return sb.ToString();
        }

        string minify(string text)
        {
            StringReader sr = new StringReader(text);
            StringBuilder sb = new StringBuilder();
            bool isMlComment = false;
            bool endsWithLetter = false;
            bool is_region = false;

            Regex reg = new Regex("^\\s*#region ([a-zA-Z0-9_\\-]+)", RegexOptions.Compiled);
            Regex regend = new Regex("^\\s*#endregion", RegexOptions.Compiled);
            Regex slComment = new Regex("\\/\\/", RegexOptions.Compiled);
            Regex mlCommentStart = new Regex("\\/\\*", RegexOptions.Compiled);
            Regex mlCommentEnd = new Regex("\\*\\/", RegexOptions.Compiled);
            Regex hash = new Regex("^\\s*#", RegexOptions.Compiled);
            
            while (true)
            {
                string line = sr.ReadLine();
                if (line == null)
                {
                    break;
                }
                // remove regions
                Match reg_match = reg.Match(line);
                if (reg_match.Success &&
                        (reg_match.Groups[1].Value == header_region ||
                         reg_match.Groups[1].Value == footer_region))
                {
                    is_region = true;
                    // skip this line
                    continue;
                }
                Match regend_match = regend.Match(line);
                if (regend_match.Success)
                {
                    is_region = false;
                    // last line we're skipping
                    continue;
                }
                if (is_region)
                {
                    // we're inside a region, skip line
                    continue;
                }

                // OK, now let's analyze the source code

                // first, get rid of the comments
                string src = line;
                Match match = slComment.Match(line);
                if (match.Success)
                {
                    line = line.Substring(0, match.Index);
                }
                // special case: if this is a #-type line, just leave it be
                var hm = hash.Match(line);
                if (hm.Success)
                {
                    if (sb.Length > 0)
                    {
                        sb.AppendLine();
                    }
                    sb.AppendLine(minifyLine(line));
                    endsWithLetter = false;
                    continue;
                }
                if (endsWithLetter)
                {
                    sb.Append(" ");
                }
                bool changed = false;
                while (true)
                {
                    bool matched = false;
                    int start = 0;
                    int end = 0;
                    match = mlCommentStart.Match(line);
                    if (match.Success)
                    {
                        matched = true;
                        start = match.Index;
                        end = match.Index + match.Length;
                        isMlComment = true;
                    }
                    match = mlCommentEnd.Match(line, start);
                    if (isMlComment && match.Success)
                    {
                        matched = true;
                        end = match.Index + match.Length;
                        isMlComment = false;
                    }
                    if (!matched)
                    {
                        break;
                    }
                    changed = true;
                    line = isMlComment ? line.Substring(0, start) :
                        line.Substring(0, start) + line.Substring(end, line.Length - end);
                }
                if (!changed && isMlComment)
                {
                    line = "";
                }
                // now, if we still have something to work with, minify the line further
                line = minifyLine(line);
                if (line.Length > 0)
                {
                    sb.Append(line);
                    endsWithLetter = char.IsLetterOrDigit(line.Last());
                }
            }
            return sb.ToString();
        }

        private void minifyBtn_Click(object sender, EventArgs e)
        {
            if (Clipboard.ContainsText())
            {
                Clipboard.SetText(minify(Clipboard.GetText()));
            }
        }
    }
}
