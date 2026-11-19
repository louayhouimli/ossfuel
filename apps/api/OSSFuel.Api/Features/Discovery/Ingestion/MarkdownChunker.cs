using System.Text.RegularExpressions;

namespace OSSFuel.Api.Features.Discovery.Ingestion;

public class MarkdownChunker : IMarkdownChunker
{
    private static readonly Regex HtmlCommentsRegex = new(
        @"<!--[\s\S]*?-->",
        RegexOptions.Compiled);

    private static readonly Regex SvgRegex = new(
        @"<svg[\s\S]*?<\/svg>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex HtmlImgRegex = new(
        @"<img\b[^>]*\/?>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex MarkdownLinkedBadgeRegex = new(
        @"\[\s*!\[(?<alt>[^\]]*)\](?:\((?<imgurl>[^\)]*)\)|\[(?<imgref>[^\]]*)\])\s*\](?:\((?<linkurl>[^\)]*)\)|\[(?<linkref>[^\]]*)\])?",
        RegexOptions.Compiled);

    private static readonly Regex MarkdownImageRegex = new(
        @"!\[(?<alt>[^\]]*)\]\((?<url>[^\)]*)\)",
        RegexOptions.Compiled);

    private static readonly Regex ShieldsUrlRegex = new(
        @"https?:\/\/(?:img\.)?shields\.io[^\s\)\>\]]*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EmptyMarkdownLinkRegex = new(
        @"\[\s*\](?:\([^\)]*\)|\[[^\]]*\])",
        RegexOptions.Compiled);

    private static readonly Regex HtmlBrRegex = new(
        @"<(?:br|hr)\b[^>]*\/?>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex HtmlTagsRegex = new(
        @"<\/?(div|p|span|a|h[1-6]|b|strong|i|em|u|ul|ol|li|table|thead|tbody|tr|th|td|details|summary|center|blockquote|pre|code|font|sub|sup|picture|source|section|article|header|footer|nav|kbd|small|figure|figcaption)\b[^>]*\/?>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ReferenceBadgeDefinitionRegex = new(
        @"^\s*\[[^\]]+\]:\s*https?:\/\/\S*(?:shields\.io|badge|travis-ci|coveralls|codecov)\S*$",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    private static readonly Regex TrailingWhitespaceRegex = new(
        @"(?m)[ \t]+$",
        RegexOptions.Compiled);

    private static readonly Regex ConsecutiveNewlinesRegex = new(
        @"\n{3,}",
        RegexOptions.Compiled);

    private static readonly Regex HeaderLineRegex = new(
        @"^(#{1,6})[ \t]+(.+)$",
        RegexOptions.Compiled);

    private static readonly string[] FeatureKeywords =
    [
        "feature",
        "capabilities",
        "capability",
        "what it does"
    ];

    private static readonly string[] ArchitectureKeywords =
    [
        "architecture",
        "design",
        "how it works",
        "internals",
        "internal"
    ];

    private static readonly string[] RoadmapKeywords =
    [
        "roadmap",
        "upcoming",
        "milestones",
        "milestone",
        "todo",
        "to-do"
    ];

    private static readonly string[] OverviewKeywords =
    [
        "about",
        "overview",
        "introduction",
        "intro",
        "description"
    ];

    public static string CleanMarkdown(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var normalized = markdown.Replace("\r\n", "\n").Replace('\r', '\n');

        // 1. Strip HTML comments
        normalized = HtmlCommentsRegex.Replace(normalized, string.Empty);

        // 2. Strip SVGs
        normalized = SvgRegex.Replace(normalized, string.Empty);

        // 3. Strip HTML <img> tags
        normalized = HtmlImgRegex.Replace(normalized, string.Empty);

        // 4. Strip linked markdown badges: [![...](...)](...)
        normalized = MarkdownLinkedBadgeRegex.Replace(normalized, string.Empty);

        // 5. Strip standalone markdown images that look like badges
        normalized = MarkdownImageRegex.Replace(normalized, match =>
        {
            var alt = match.Groups["alt"].Value;
            var url = match.Groups["url"].Value;
            return IsBadge(alt, url) ? string.Empty : match.Value;
        });

        // 6. Strip raw shields.io urls and dangling empty markdown links
        normalized = ShieldsUrlRegex.Replace(normalized, string.Empty);
        normalized = EmptyMarkdownLinkRegex.Replace(normalized, string.Empty);

        // 7. Strip reference-style badge definitions
        normalized = ReferenceBadgeDefinitionRegex.Replace(normalized, string.Empty);

        // 8. Replace line break tags with newlines
        normalized = HtmlBrRegex.Replace(normalized, "\n");

        // 9. Strip raw HTML tags while keeping inner text
        normalized = HtmlTagsRegex.Replace(normalized, string.Empty);

        // 10. Collapse horizontal and vertical whitespace
        normalized = TrailingWhitespaceRegex.Replace(normalized, string.Empty);
        normalized = ConsecutiveNewlinesRegex.Replace(normalized, "\n\n");

        return normalized.Trim();
    }

    public IReadOnlyList<MarkdownChunkResult> ChunkMarkdown(
        string? markdown,
        int maxChunkChars = 1200,
        int overlapChars = 150)
    {
        var cleaned = CleanMarkdown(markdown);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return Array.Empty<MarkdownChunkResult>();
        }

        var lines = cleaned.Split('\n');
        var sections = new List<MarkdownSection>();
        var headerStack = new List<(int Level, string Token)>();
        var currentSection = new MarkdownSection { IsTop = true, HeaderPath = null, HeaderTitle = null };
        var inCodeBlock = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                inCodeBlock = !inCodeBlock;
                currentSection.Lines.Add(line);
                continue;
            }

            if (!inCodeBlock)
            {
                var match = HeaderLineRegex.Match(line);
                if (match.Success)
                {
                    var level = match.Groups[1].Value.Length;
                    var title = match.Groups[2].Value.Trim();

                    if (currentSection.HasContent())
                    {
                        sections.Add(currentSection);
                    }

                    headerStack.RemoveAll(h => h.Level >= level);
                    headerStack.Add((level, $"{new string('#', level)} {title}"));

                    var currentHeaderPath = string.Join(" > ", headerStack.Select(h => h.Token));

                    currentSection = new MarkdownSection
                    {
                        HeaderPath = currentHeaderPath,
                        HeaderTitle = title,
                        HeaderLevel = level,
                        IsTop = false
                    };
                    continue;
                }
            }

            currentSection.Lines.Add(line);
        }

        if (currentSection.HasContent())
        {
            sections.Add(currentSection);
        }

        if (sections.Count == 0)
        {
            return Array.Empty<MarkdownChunkResult>();
        }

        var results = new List<MarkdownChunkResult>();

        for (var i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            var sectionContent = string.Join("\n", section.Lines).Trim();
            if (string.IsNullOrWhiteSpace(sectionContent))
            {
                continue;
            }

            var isTopSection = section.IsTop || (i == 0 && section.HeaderLevel == 1);
            var chunkType = DetermineChunkType(section.HeaderPath, section.HeaderTitle, sectionContent, isTopSection);

            var chunked = SplitContent(sectionContent, chunkType, section.HeaderPath, maxChunkChars, overlapChars);
            results.AddRange(chunked);
        }

        return results;
    }

    private static bool IsBadge(string alt, string url)
    {
        if (string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(alt))
        {
            return true;
        }

        var altLower = alt.ToLowerInvariant();
        var urlLower = url.ToLowerInvariant();

        return urlLower.Contains("shields.io")
            || urlLower.Contains("shield")
            || urlLower.Contains("badge")
            || urlLower.Contains("badgen.net")
            || urlLower.Contains("travis-ci")
            || urlLower.Contains("coveralls")
            || urlLower.Contains("codecov")
            || urlLower.Contains("actions/workflows")
            || urlLower.Contains("workflows/")
            || urlLower.Contains(".svg")
            || urlLower.Contains("fury.io")
            || urlLower.Contains("crates.io/crates")
            || urlLower.Contains("sonarcloud.io")
            || urlLower.Contains("circleci.com")
            || altLower.Contains("badge")
            || altLower.Contains("build")
            || altLower.Contains("license")
            || altLower.Contains("npm")
            || altLower.Contains("coverage")
            || altLower.Contains("status")
            || altLower.Contains("ci")
            || altLower.Contains("version")
            || altLower.Contains("downloads")
            || altLower.Contains("stars")
            || altLower.Contains("sponsor");
    }

    private static string DetermineChunkType(
        string? headerPath,
        string? headerTitle,
        string content,
        bool isTopSection)
    {
        // 1. Check innermost header title first
        if (ContainsAny(headerTitle, FeatureKeywords))
        {
            return ProjectChunkType.Features;
        }

        if (ContainsAny(headerTitle, ArchitectureKeywords))
        {
            return ProjectChunkType.Architecture;
        }

        if (ContainsAny(headerTitle, RoadmapKeywords))
        {
            return ProjectChunkType.Roadmap;
        }

        if (ContainsAny(headerTitle, OverviewKeywords))
        {
            return ProjectChunkType.Overview;
        }

        // 2. Check full hierarchical header path
        if (ContainsAny(headerPath, FeatureKeywords))
        {
            return ProjectChunkType.Features;
        }

        if (ContainsAny(headerPath, ArchitectureKeywords))
        {
            return ProjectChunkType.Architecture;
        }

        if (ContainsAny(headerPath, RoadmapKeywords))
        {
            return ProjectChunkType.Roadmap;
        }

        if (ContainsAny(headerPath, OverviewKeywords))
        {
            return ProjectChunkType.Overview;
        }

        // 3. Top of document defaults to Overview
        if (isTopSection)
        {
            return ProjectChunkType.Overview;
        }

        // 4. Check section body content for domain signals
        if (ContainsAny(content, FeatureKeywords))
        {
            return ProjectChunkType.Features;
        }

        if (ContainsAny(content, ArchitectureKeywords))
        {
            return ProjectChunkType.Architecture;
        }

        if (ContainsAny(content, RoadmapKeywords))
        {
            return ProjectChunkType.Roadmap;
        }

        if (ContainsAny(content, OverviewKeywords))
        {
            return ProjectChunkType.Overview;
        }

        // 5. Default to General
        return ProjectChunkType.General;
    }

    private static bool ContainsAny(string? text, string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        foreach (var keyword in keywords)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<MarkdownChunkResult> SplitContent(
        string content,
        string chunkType,
        string? headerPath,
        int maxChunkChars,
        int overlapChars)
    {
        var results = new List<MarkdownChunkResult>();
        content = content.Trim();

        if (string.IsNullOrWhiteSpace(content))
        {
            return results;
        }

        if (maxChunkChars <= 0)
        {
            maxChunkChars = 1200;
        }

        if (overlapChars < 0)
        {
            overlapChars = 0;
        }

        if (overlapChars >= maxChunkChars)
        {
            overlapChars = maxChunkChars / 4;
        }

        if (content.Length <= maxChunkChars)
        {
            results.Add(new MarkdownChunkResult(chunkType, content, headerPath));
            return results;
        }

        var startIndex = 0;
        var totalLength = content.Length;

        while (startIndex < totalLength)
        {
            var remaining = totalLength - startIndex;
            if (remaining <= maxChunkChars)
            {
                var finalChunk = content.Substring(startIndex).Trim();
                if (!string.IsNullOrWhiteSpace(finalChunk))
                {
                    results.Add(new MarkdownChunkResult(chunkType, finalChunk, headerPath));
                }
                break;
            }

            var maxEnd = Math.Min(startIndex + maxChunkChars, totalLength);
            var minEnd = startIndex + Math.Max(1, (int)(maxChunkChars * 0.4));

            var endIndex = FindBestBreak(content, minEnd, maxEnd);
            if (endIndex <= startIndex)
            {
                endIndex = maxEnd;
            }

            var chunkText = content.Substring(startIndex, endIndex - startIndex).Trim();
            if (!string.IsNullOrWhiteSpace(chunkText))
            {
                results.Add(new MarkdownChunkResult(chunkType, chunkText, headerPath));
            }

            if (endIndex >= totalLength)
            {
                break;
            }

            if (overlapChars > 0)
            {
                var targetStart = endIndex - overlapChars;
                if (targetStart <= startIndex)
                {
                    startIndex = endIndex;
                }
                else
                {
                    var searchLength = Math.Min(endIndex - targetStart, totalLength - targetStart);
                    var nextWord = content.IndexOfAny([' ', '\n', '\t'], targetStart, searchLength);
                    if (nextWord != -1 && nextWord + 1 < endIndex)
                    {
                        startIndex = nextWord + 1;
                    }
                    else
                    {
                        startIndex = targetStart;
                    }
                }
            }
            else
            {
                startIndex = endIndex;
            }
        }

        return results;
    }

    private static int FindBestBreak(string content, int minEnd, int maxEnd)
    {
        var searchStart = Math.Min(maxEnd - 1, content.Length - 1);
        if (searchStart < minEnd)
        {
            return maxEnd;
        }

        var count = searchStart - minEnd + 1;

        // 1. Paragraph break: "\n\n"
        var pBreak = content.LastIndexOf("\n\n", searchStart, count, StringComparison.Ordinal);
        if (pBreak >= minEnd)
        {
            return pBreak;
        }

        // 2. Sentence boundary
        var sBreak = FindLastSentenceEnd(content, minEnd, searchStart, count);
        if (sBreak >= minEnd)
        {
            return sBreak;
        }

        // 3. Line break: '\n'
        var lBreak = content.LastIndexOf('\n', searchStart, count);
        if (lBreak >= minEnd)
        {
            return lBreak;
        }

        // 4. Word boundary: ' '
        var wBreak = content.LastIndexOf(' ', searchStart, count);
        if (wBreak >= minEnd)
        {
            return wBreak;
        }

        return maxEnd;
    }

    private static int FindLastSentenceEnd(string content, int minEnd, int searchStart, int count)
    {
        var bestIndex = -1;
        string[] sentenceEndings = [". ", "? ", "! ", ".\n", "?\n", "!\n"];

        foreach (var ending in sentenceEndings)
        {
            var idx = content.LastIndexOf(ending, searchStart, count, StringComparison.Ordinal);
            if (idx >= minEnd)
            {
                var breakPoint = idx + 1;
                if (breakPoint > bestIndex)
                {
                    bestIndex = breakPoint;
                }
            }
        }

        return bestIndex;
    }

    private sealed class MarkdownSection
    {
        public string? HeaderPath { get; set; }
        public string? HeaderTitle { get; set; }
        public int HeaderLevel { get; set; }
        public bool IsTop { get; set; }
        public List<string> Lines { get; } = new();

        public bool HasContent() => Lines.Any(l => !string.IsNullOrWhiteSpace(l));
    }
}
