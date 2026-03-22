using CommunityToolkit.Mvvm.ComponentModel;

namespace SchemaHammer.ViewModels.Editors;

public abstract class EditorBaseViewModel : ObservableObject
{
    public abstract string EditorTitle { get; }
}
