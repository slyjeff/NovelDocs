﻿using NovelDocs.Entity;
using NovelDocs.PageControls;

namespace NovelDocs.Pages.CharacterDetails; 

public abstract class CharacterDetailsViewModel : ViewModel {
    private Character _character = new();

    internal void SetCharacter(Character character) {
        _character = character;
    }

    public virtual string Name {
        get => _character.Name;
        set => _character.Name = value;
    }

    public string ImageUriSource => _character.ImageUriSource;

    public bool IsDocumentAssigned => !string.IsNullOrEmpty(_character.GoogleDocId);
}