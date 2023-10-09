﻿using System.Collections.Generic;

namespace NovelDocs.Entity; 

public enum ManuscriptElementType { Section, Paragraph, Scene }

public sealed class ManuscriptElement {
    public string Name { get; set; } = string.Empty;
    
    public ManuscriptElementType Type { get; set; }

    public string GoogleDocId { get; set; } = string.Empty;

    public IList<ManuscriptElement> ManuscriptElements = new List<ManuscriptElement>();
}