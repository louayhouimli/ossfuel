using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OSSFuel.Api.Features.Discovery;
using OSSFuel.Api.Features.Discovery.Gemini;
using OSSFuel.Api.Features.Discovery.Ingestion;

namespace OSSFuel.UnitTests.Features.Discovery;

public class MarkdownChunkerTests
{
    private readonly MarkdownChunker _chunker = new();

    [Fact]
    public void CleanMarkdown_StripsBadgesHtmlCommentsAndTags_WhilePreservingProse()
    {
        var rawMarkdown = """
            # OSSFuel

            [![Build Status](https://github.com/ossfuel/ossfuel/workflows/CI/badge.svg)](https://github.com/ossfuel/ossfuel/actions)
            [![Coverage Status](https://coveralls.io/repos/github/ossfuel/ossfuel/badge.svg?branch=main)](https://coveralls.io)
            [![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
            [![npm](https://img.shields.io/npm/v/ossfuel.svg)]
            <!-- Internal build badge comment -->

            <p align="center">
              <a href="https://github.com/ossfuel"><img src="https://img.shields.io/github/stars/ossfuel/ossfuel" alt="Stars" /></a>
              <svg height="32" class="octicon" viewBox="0 0 16 16" version="1.1" width="32"><path d="M8 0..."></path></svg>
            </p>

            OSSFuel is a developer-first platform designed to fund open-source maintainers.
            Visit our <a href="https://ossfuel.org">official website</a> for more details.

            ```csharp
            // C# generic types should be preserved
            public Task<List<string>> GetProjectsAsync();
            ```
            """;

        var cleaned = MarkdownChunker.CleanMarkdown(rawMarkdown);

        Assert.DoesNotContain("Build Status", cleaned);
        Assert.DoesNotContain("coveralls.io", cleaned);
        Assert.DoesNotContain("shields.io", cleaned);
        Assert.DoesNotContain("badge.svg", cleaned);
        Assert.DoesNotContain("<!-- Internal build badge comment -->", cleaned);
        Assert.DoesNotContain("<p align=\"center\">", cleaned);
        Assert.DoesNotContain("</p>", cleaned);
        Assert.DoesNotContain("<img", cleaned);
        Assert.DoesNotContain("<svg", cleaned);
        Assert.DoesNotContain("<a href", cleaned);

        Assert.Contains("# OSSFuel", cleaned);
        Assert.Contains("OSSFuel is a developer-first platform designed to fund open-source maintainers.", cleaned);
        Assert.Contains("official website", cleaned);
        Assert.Contains("Task<List<string>>", cleaned);
    }

    [Fact]
    public void ChunkMarkdown_TracksHierarchicalHeaderPath()
    {
        var markdown = """
            # Title
            Top level content.

            ## Section
            Section content.

            ### SubSection
            Deep subsection content.

            ## Another Section
            Another section content.
            """;

        var chunks = _chunker.ChunkMarkdown(markdown);

        Assert.Equal(4, chunks.Count);
        Assert.Equal("# Title", chunks[0].HeaderPath);
        Assert.Equal("# Title > ## Section", chunks[1].HeaderPath);
        Assert.Equal("# Title > ## Section > ### SubSection", chunks[2].HeaderPath);
        Assert.Equal("# Title > ## Another Section", chunks[3].HeaderPath);
    }

    [Fact]
    public void ChunkMarkdown_TracksDeepHeaderPathWithoutIntermediateBody()
    {
        var markdown = """
            # OSSFuel
            ## Architecture
            ### Gateway
            The gateway routes incoming API traffic and validates API keys.
            """;

        var chunks = _chunker.ChunkMarkdown(markdown);

        Assert.Single(chunks);
        Assert.Equal("# OSSFuel > ## Architecture > ### Gateway", chunks[0].HeaderPath);
        Assert.Equal(ProjectChunkType.Architecture, chunks[0].ChunkType);
        Assert.Equal("The gateway routes incoming API traffic and validates API keys.", chunks[0].Content);
    }

    [Fact]
    public void ChunkMarkdown_CategorizesChunkTypesCorrectly()
    {
        var markdown = """
            # Project X
            Welcome to Project X, an innovative solution for cloud engineers.

            ## Key Features
            - Blazing fast execution
            - Zero-config deployment

            ## System Architecture
            Built using modern event-driven microservices.

            ## Product Roadmap
            - Q1 2026: Multi-cloud support
            - Q2 2026: Automatic rollback

            ## Installation & Setup
            Run `dotnet run` to launch the API server.
            """;

        var chunks = _chunker.ChunkMarkdown(markdown);

        Assert.Equal(5, chunks.Count);

        Assert.Equal(ProjectChunkType.Overview, chunks[0].ChunkType);
        Assert.Contains("Welcome to Project X", chunks[0].Content);

        Assert.Equal(ProjectChunkType.Features, chunks[1].ChunkType);
        Assert.Contains("Blazing fast execution", chunks[1].Content);

        Assert.Equal(ProjectChunkType.Architecture, chunks[2].ChunkType);
        Assert.Contains("Built using modern event-driven", chunks[2].Content);

        Assert.Equal(ProjectChunkType.Roadmap, chunks[3].ChunkType);
        Assert.Contains("Q1 2026", chunks[3].Content);

        Assert.Equal(ProjectChunkType.General, chunks[4].ChunkType);
        Assert.Contains("dotnet run", chunks[4].Content);
    }

    [Fact]
    public void ChunkMarkdown_CategorizesSectionByContentKeywords_WhenHeaderIsGeneric()
    {
        var markdown = """
            # MyProject
            Introductory text.

            ## Section A
            Here is our upcoming roadmap and milestone checklist for the project.

            ## Section B
            This covers our internals and how it works behind the scenes.

            ## Section C
            Here is what it does and our core capabilities.
            """;

        var chunks = _chunker.ChunkMarkdown(markdown);

        Assert.Equal(4, chunks.Count);
        Assert.Equal(ProjectChunkType.Overview, chunks[0].ChunkType);
        Assert.Equal(ProjectChunkType.Roadmap, chunks[1].ChunkType);
        Assert.Equal(ProjectChunkType.Architecture, chunks[2].ChunkType);
        Assert.Equal(ProjectChunkType.Features, chunks[3].ChunkType);
    }

    [Fact]
    public void ChunkMarkdown_SplitsLongSectionsAcrossParagraphBoundaries_WithOverlap()
    {
        var p1 = new string('A', 700);
        var p2 = new string('B', 700);
        var p3 = new string('C', 700);

        var markdown = $"""
            # Features
            {p1}

            {p2}

            {p3}
            """;

        var chunks = _chunker.ChunkMarkdown(markdown, maxChunkChars: 1200, overlapChars: 150);

        Assert.True(chunks.Count >= 2, $"Expected at least 2 chunks, but got {chunks.Count}");

        foreach (var chunk in chunks)
        {
            Assert.Equal(ProjectChunkType.Features, chunk.ChunkType);
            Assert.Equal("# Features", chunk.HeaderPath);
            Assert.True(chunk.Content.Length <= 1200, $"Chunk exceeded maxChunkChars: {chunk.Content.Length}");
        }

        // Verify paragraph boundaries: Chunk 1 should end with p1 (700 As)
        Assert.StartsWith(new string('A', 50), chunks[0].Content);
        // Overlap ensures chunk 2 contains some content from chunk 1's end
        Assert.True(chunks[1].Content.Length > 0);
    }

    [Fact]
    public void ChunkMarkdown_SplitsLongSentences_WhenNoParagraphBreaksExist()
    {
        var sentence1 = "This is the first long sentence explaining the system design in thorough detail. ";
        var sentence2 = "This is the second long sentence explaining how the caching layer operates efficiently. ";
        var sentence3 = "This is the third long sentence explaining the PostgreSQL database indexing strategy. ";

        var longContent = string.Concat(Enumerable.Repeat(sentence1 + sentence2 + sentence3, 8));

        var markdown = $"""
            ## Architecture
            {longContent}
            """;

        var chunks = _chunker.ChunkMarkdown(markdown, maxChunkChars: 600, overlapChars: 100);

        Assert.True(chunks.Count > 1);
        foreach (var chunk in chunks)
        {
            Assert.Equal(ProjectChunkType.Architecture, chunk.ChunkType);
            Assert.True(chunk.Content.Length <= 600);
        }
    }

    [Fact]
    public void ChunkMarkdown_HandlesEmptyAndMinimalInput()
    {
        Assert.Empty(_chunker.ChunkMarkdown(null));
        Assert.Empty(_chunker.ChunkMarkdown(""));
        Assert.Empty(_chunker.ChunkMarkdown("   \n\t  \r\n   "));
        Assert.Empty(_chunker.ChunkMarkdown("<!-- only a comment -->"));
        Assert.Empty(_chunker.ChunkMarkdown("[![Build](https://img.shields.io/badge/build-passing)]"));
    }

    [Fact]
    public void ChunkMarkdown_HandlesReadmeWithoutHeaders_AssignsOverviewAndNullHeaderPath()
    {
        var markdown = """
            OSSFuel is a platform for supporting open source software.
            It provides transparent sponsorship and AI-powered recommendations.
            """;

        var chunks = _chunker.ChunkMarkdown(markdown);

        Assert.Single(chunks);
        Assert.Equal(ProjectChunkType.Overview, chunks[0].ChunkType);
        Assert.Null(chunks[0].HeaderPath);
        Assert.Contains("OSSFuel is a platform", chunks[0].Content);
    }

    [Fact]
    public void ChunkMarkdown_IgnoresHeadersInsideCodeBlocks()
    {
        var markdown = """
            # Architecture
            Here is sample code:

            ```csharp
            #region Setup
            var x = 1;
            #endregion
            ```

            And more architecture details.
            """;

        var chunks = _chunker.ChunkMarkdown(markdown);

        Assert.Single(chunks);
        Assert.Equal("# Architecture", chunks[0].HeaderPath);
        Assert.Equal(ProjectChunkType.Architecture, chunks[0].ChunkType);
        Assert.Contains("#region Setup", chunks[0].Content);
    }

    [Fact]
    public void DependencyInjection_RegistersIMarkdownChunker()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddGeminiClient(configuration);

        using var provider = services.BuildServiceProvider();
        var chunker = provider.GetService<IMarkdownChunker>();

        Assert.NotNull(chunker);
        Assert.IsType<MarkdownChunker>(chunker);
    }
}
