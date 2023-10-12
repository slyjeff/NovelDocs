﻿using System;
using System.Windows.Input;
using NovelDocs.Entity;
using NovelDocs.Extensions;
using NovelDocs.PageControls;
using NovelDocs.Pages.CharacterDetails;
using NovelDocs.Pages.GoogleDoc;
using NovelDocs.Pages.NovelDetails;
using NovelDocs.Pages.SceneDetails;
using NovelDocs.Pages.SectionDetails;
using NovelDocs.Services;

namespace NovelDocs.Pages.NovelEdit; 

internal sealed class NovelEditController : Controller<NovelEditView, NovelEditViewModel> {
    private readonly IServiceProvider _serviceProvider;
    private readonly IDataPersister _dataPersister;
    private Action _novelClosed = null!; //will never be null because initialize will always be called
    private Novel _novel = null!; //will never be null because initialize will always be called

    public NovelEditController(IServiceProvider serviceProvider, IDataPersister dataPersister, IGoogleDocController googleDocController) {
        _serviceProvider = serviceProvider;
        _dataPersister = dataPersister;

        ViewModel.GoogleDocView = googleDocController.View;

        View.OnMoveNovelTreeItem += MoveNovelTreeItem;
    }

    public void Initialize(Action novelClosed, Novel novelToLoad) {
        _novelClosed = novelClosed;
        _novel = novelToLoad;

        _dataPersister.Data.LastOpenedNovel = novelToLoad.Name;
        _dataPersister.Save();

        ViewModel.Manuscript.Selected += ManuscriptSelected;
        ViewModel.Characters.Selected += CharactersSelected;

        foreach (var element in _novel.ManuscriptElements) {
            if (element.Type != ManuscriptElementType.Section) {
                continue;
            }

            var treeItem = new ManuscriptElementTreeItem(element, ViewModel, ManuscriptElementSelected);
            ViewModel.Manuscript.ManuscriptElements.Add(treeItem);
        }

        ViewModel.Manuscript.IsSelected = true;

        foreach (var character in _novel.Characters) {
            var treeItem = new CharacterTreeItem(character, CharacterSelected);
            ViewModel.Characters.Characters.Add(treeItem);
        }
    }

    private void MoveNovelTreeItem(NovelTreeItem itemToMove, MoveType moveType, NovelTreeItem destinationItem) {
        switch (itemToMove) {
            case CharacterTreeItem characterToMove:
                MoveCharacter(characterToMove, destinationItem as CharacterTreeItem);
                break;
            case ManuscriptElementTreeItem manuscriptItemToMove:
                MoveManuscriptElement(manuscriptItemToMove, moveType, destinationItem as ManuscriptElementTreeItem);
                break;
        }
    }

    private void MoveCharacter(CharacterTreeItem treeItemToMove, CharacterTreeItem? destinationTreeItem) {
        if (destinationTreeItem == null) {
            return;
        }

        _novel.Characters.Move(treeItemToMove.Character, destinationTreeItem.Character);
        ViewModel.Characters.Characters.Move(treeItemToMove, destinationTreeItem);

        _dataPersister.Save();

        treeItemToMove.IsSelected = true;
    }

    private void MoveManuscriptElement(ManuscriptElementTreeItem treeItemToMove, MoveType moveDestination, ManuscriptElementTreeItem? destinationTreeItem) {
        if (destinationTreeItem == null) {
            MoveManuscriptElementToRoot(treeItemToMove);
        } else if (moveDestination == MoveType.Into) {
            MoveManuscriptElementIntoSection(treeItemToMove, destinationTreeItem);
        } else {
            MoveManuscriptElementBeforeAnotherManuscriptElement(treeItemToMove, destinationTreeItem);
        }
    }

    private void MoveManuscriptElementBeforeAnotherManuscriptElement(ManuscriptElementTreeItem treeItemToMove, ManuscriptElementTreeItem destinationTreeItem) {
        var destinationParentList = _novel.ManuscriptElements.FindParentManuscriptElementList(destinationTreeItem.ManuscriptElement);
        if (destinationParentList == null) {
            return;
        }

        //first move the item to the new list, then move it to before the existing item
        _novel.ManuscriptElements.MoveManuscriptElementToList(treeItemToMove.ManuscriptElement, destinationParentList);
        destinationParentList.Move(treeItemToMove.ManuscriptElement, destinationTreeItem.ManuscriptElement);

        var treeItemDestinationParentList = destinationTreeItem.Parent?.ManuscriptElements ?? ViewModel.Manuscript.ManuscriptElements;
        ViewModel.Manuscript.ManuscriptElements.MoveManuscriptElementTreeItemToList(treeItemToMove, treeItemDestinationParentList);
        treeItemDestinationParentList.Move(treeItemToMove, destinationTreeItem);

        treeItemToMove.Parent = destinationTreeItem.Parent;

        _dataPersister.Save();

        treeItemToMove.IsSelected = true;
        if (treeItemToMove.Parent != null) {
            treeItemToMove.Parent.IsExpanded = true;
        }
    }


    private void MoveManuscriptElementToRoot(ManuscriptElementTreeItem treeItemToMove) {
        //this item can't be at the root already
        if (treeItemToMove.Parent == null) {
            return;
        }

        _novel.ManuscriptElements.MoveManuscriptElementToList(treeItemToMove.ManuscriptElement, _novel.ManuscriptElements);

        treeItemToMove.Parent.ManuscriptElements.Remove(treeItemToMove);
        ViewModel.Manuscript.ManuscriptElements.Add(treeItemToMove);
        treeItemToMove.Parent = null;

        _dataPersister.Save();

        treeItemToMove.IsSelected = true;
    }

    private void MoveManuscriptElementIntoSection(ManuscriptElementTreeItem treeItemToMove, ManuscriptElementTreeItem destinationTreeItem) {
        _novel.ManuscriptElements.MoveManuscriptElementToList(treeItemToMove.ManuscriptElement, destinationTreeItem.ManuscriptElement.ManuscriptElements);

        var treeItemParentList = treeItemToMove.Parent?.ManuscriptElements ?? ViewModel.Manuscript.ManuscriptElements;
        treeItemParentList.Remove(treeItemToMove);

        destinationTreeItem.ManuscriptElements.Add(treeItemToMove);
        treeItemToMove.Parent = destinationTreeItem;

        _dataPersister.Save();

        treeItemToMove.IsSelected = true;
        destinationTreeItem.IsExpanded = true;
    }



    private void ManuscriptSelected() {
        var novelDetailsController = _serviceProvider.CreateInstance<NovelDetailsController>();
        novelDetailsController.Initialize(_novel);
        ViewModel.EditDataView = novelDetailsController.View;
    }

    private async void ManuscriptElementSelected(ManuscriptElementTreeItem treeItem) {
        if (treeItem.ManuscriptElement.Type == ManuscriptElementType.Section) {
            var novelDetailsController = _serviceProvider.CreateInstance<SectionDetailsController>();
            novelDetailsController.Initialize(treeItem);
            ViewModel.EditDataView = novelDetailsController.View;
            return;
        }

        var sceneDetailsController = _serviceProvider.CreateInstance<SceneDetailsController>();
        await sceneDetailsController.Initialize(treeItem);
        ViewModel.EditDataView = sceneDetailsController.View;
    }

    private void CharactersSelected() {
        ViewModel.EditDataView = null!;
    }

    private async void CharacterSelected(CharacterTreeItem treeItem) {
        var characterDetailsController = _serviceProvider.CreateInstance<CharacterDetailsController>();
        await characterDetailsController.Initialize(treeItem);
        ViewModel.EditDataView = characterDetailsController.View;
    }

    private void AddManuscriptElement(ManuscriptElementTreeItem? parent, ManuscriptElement newManuscriptElement) {
        var newTreeItem = new ManuscriptElementTreeItem(newManuscriptElement, ViewModel, ManuscriptElementSelected) {
            Parent = parent
        };
        if (parent == null) {
            _novel.ManuscriptElements.Add(newManuscriptElement);
            ViewModel.Manuscript.ManuscriptElements.Add(newTreeItem);
        } else {
            parent.ManuscriptElement.ManuscriptElements.Add(newManuscriptElement);
            parent.ManuscriptElements.Add(newTreeItem);
        }

        _dataPersister.Save();

        newTreeItem.IsSelected = true;
    }

    [Command]
    public void CloseNovel() {
        _dataPersister.Data.LastOpenedNovel = string.Empty;
        _dataPersister.Save();
        _novelClosed.Invoke();
    }

    [Command]
    public void AddSection(ManuscriptElementTreeItem? parent) {
        var section = new ManuscriptElement {
            Name = "New Section",
            Type = ManuscriptElementType.Section
        };

        AddManuscriptElement(parent, section);
    }

    [Command]
    public void AddScene(ManuscriptElementTreeItem? parent) {
        var scene = new ManuscriptElement {
            Name = "New Scene",
            Type = ManuscriptElementType.Scene
        };

        AddManuscriptElement(parent, scene);
    }

    [Command]
    public void DeleteManuscriptElement(ManuscriptElementTreeItem itemToDelete) {
        if (itemToDelete.Parent == null) {
            _novel.ManuscriptElements.Remove(itemToDelete.ManuscriptElement);
            ViewModel.Manuscript.ManuscriptElements.Remove(itemToDelete);
        } else {
            var parent = itemToDelete.Parent;
            parent.ManuscriptElement.ManuscriptElements.Remove(itemToDelete.ManuscriptElement);
            parent.ManuscriptElements.Remove(itemToDelete);
        }

        _dataPersister.Save();
    }

    [Command]
    public void AddCharacter() {
        var character = new Character {
            Name = "New Character",
        };
        _novel.Characters.Add(character);
        _dataPersister.Save();

        var treeItem = new CharacterTreeItem(character, CharacterSelected);
        ViewModel.Characters.Characters.Add(treeItem);

        treeItem.IsSelected = true;
    }
}