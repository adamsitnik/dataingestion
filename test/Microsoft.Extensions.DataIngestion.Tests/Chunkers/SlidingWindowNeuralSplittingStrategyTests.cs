// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Chunkers.Tests
{
    public class SlidingWindowNeuralSplittingStrategyTests : TextSplittingStrategyTests
    {
        protected override TextSplittingStrategy GetTextSplittingStrategy()
        {
            string vocabularyPath = Environment.GetEnvironmentVariable("BERT3_VOCABULARY")!;
            string modelPath = Environment.GetEnvironmentVariable("BERT3_MODEL")!;
            return new SlidingWindowNeuralSplittingStrategy(vocabularyPath, modelPath);
        }

        [Fact]
        public async Task ExampleFromModelAuthor()
        {
            IngestionDocument doc = new IngestionDocument("doc");
            doc.Sections.Add(new IngestionDocumentSection
            {
                Elements =
                {
                    new IngestionDocumentParagraph("The causes and effects of dropouts in vocational and professional education are more pressing than ever. A decreasing attractiveness of vocational education, particularly in payment and quality, causes higher dropout rates while hitting ongoing demographic changes resulting in extensive skill shortages for many regions. Therefore, tackling the internationally high dropout rates is of utmost political and scientific interest. This thematic issue contributes to the conceptualization, analysis, and prevention of vocational and professional dropouts by bringing together current research that progresses to a deeper processual understanding and empirical modelling of dropouts. It aims to expand our understanding of how dropout and decision processes leading to dropout can be conceptualized and measured in vocational and professional contexts. Another aim is to gather empirical studies on both predictors and dropout consequences. Based on this knowledge, the thematic issue intends to provide evidence of effective interventions to avoid dropouts and identify promising ways for future dropout research in professional and vocational education to support evidence-based vocational education policy.\r\n\r\nWe thus welcome research contributions (original empirical and conceptual/measurement-related articles, literature reviews, meta-analyses) on dropouts (e.g., premature terminations, intentions to terminate, vertical and horizontal dropouts) that are situated in vocational and professional education at workplaces, schools, or other tertiary professional education institutions. \r\n\r\n\r\nPart 1 of the thematic series outlines central theories and measurement concepts for vocational and professional dropouts. Part 2 outlines measurement approaches for dropout. Part 3 investigates relevant predictors of dropout. Part 4 analyzes the effects of dropout on an individual, organizational, and systemic level. Part 5 deals with programs and interventions for the prevention of dropouts.\r\n\r\nWe welcome papers that include but are not limited to:\r\n\r\nTheoretical papers on the concept and processes of vocational and professional dropout or retention\r\nMeasurement approaches to assess dropout or retention\r\nQuantitative and qualitative papers on the causes of dropout or retention\r\nQuantitative and qualitative papers on the effects of dropout or retention on learners, providers/organizations and the (educational) system\r\nDesign-based research and experimental papers on dropout prevention programs or retention\r\nSubmission instructions\r\nBefore submitting your manuscript, please ensure you have carefully read the Instructions for Authors for Empirical Research in Vocational Education and Training. The complete manuscript should be submitted through the Empirical Research in Vocational Education and Training submission system. To ensure that you submit to the correct thematic series please select the appropriate section in the drop-down menu upon submission. In addition, indicate within your cover letter that you wish your manuscript to be considered as part of the thematic series on series title. All submissions will undergo rigorous peer review, and accepted articles will be published within the journal as a collection.\r\n\r\nLead Guest Editor:\r\nProf. Dr. Viola Deutscher, University of Mannheim\r\nviola.deutscher@uni-mannheim.de\r\n\r\nGuest Editors:\r\nProf. Dr. Stefanie Findeisen, University of Konstanz\r\nstefanie.findeisen@uni-konstanz.de \r\n\r\nProf. Dr. Christian Michaelis, Georg-August-University of Göttingen\r\nchristian.michaelis@wiwi.uni-goettingen.de\r\n\r\nDeadline for submission\r\nThis Call for Papers is open from now until 29 February 2023. Submitted papers will be reviewed in a timely manner and published directly after acceptance (i.e., without waiting for the accomplishment of all other contributions). Thanks to the Empirical Research in Vocational Education and Training (ERVET) open access policy, the articles published in this thematic issue will have a wide, global audience.\r\n\r\nOption of submitting abstracts: Interested authors should submit a letter of intent including a working title for the manuscript, names, affiliations, and contact information for all authors, and an abstract of no more than 500 words to the lead guest editor Viola Deutscher (viola.deutscher@uni-mannheim.de) by July, 31st 2023. Due to technical issues, we also ask authors who already submitted an abstract before May, 30th to send their abstracts again to the address stated above. However, abstract submission is optional and is not mandatory for the full paper submission.\r\n\r\nDifferent dropout directions in vocational education and training: the role of the initiating party and trainees’ reasons for dropping out\r\nThe high rates of premature contract termination (PCT) in vocational education and training (VET) programs have led to an increasing number of studies examining the reasons why adolescents drop out. Since adol...\r\n\r\nAuthors:Christian Michaelis and Stefanie Findeisen\r\nCitation:Empirical Research in Vocational Education and Training 2024 16:15\r\nContent type:Research\r\nPublished on: 6 August 2024\"")
                }
            });

            IngestionChunker<string> chunker = GetStrategyChunker();
            IReadOnlyList<IngestionChunk<string>> chunks = await chunker.ProcessAsync(doc).ToListAsync();
            Assert.Equal(4, chunks.Count);
        }
    }
}
