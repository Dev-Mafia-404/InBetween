using UnityEngine;

public interface IInteractable 
{
  Transform Transform { get; }
  string DisplayText { get; }
  bool CanInteract { get; }
  void OnInteract(PlayerInteractor interactor);
  void OnFocusEnter(PlayerInteractor interactor);
  void OnFocusExit(PlayerInteractor interactor);
}