// SPDX-FileCopyrightText: 2024 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 JohnOakman <sremy2012@hotmail.fr>
// SPDX-FileCopyrightText: 2025 deltanedas <39013340+deltanedas@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 deltanedas <@deltanedas:kde.org>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.UserInterface.Controls;
using Content.Shared._Shitmed.Autodoc;
using Content.Shared._Shitmed.Autodoc.Components;
using Content.Shared._Shitmed.Autodoc.Systems;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Timing;
using System.IO;

namespace Content.Client._Shitmed.Autodoc;

[GenerateTypedNameReferences]
public sealed partial class AutodocProgramWindow : FancyWindow
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IFileDialogManager _dialogManager = default!;
    [Dependency] private readonly ILogManager _logMan = default!;
    [Dependency] private readonly ISerializationManager _serMan = default!;
    private SharedAutodocSystem _autodoc = default!;

    public event Action? OnToggleSafety;
    public event Action? OnRemoveProgram;
    public event Action<IAutodocStep, int>? OnAddStep;
    public event Action<int>? OnRemoveStep;
    public event Action? OnStart;

    private EntityUid _owner;
    private AutodocProgram _program;
    private int _steps;
    private bool _safety = true;
    private ISawmill _sawmill;

    private int? _selected;
    private AddStepWindow? _addStep;

    public AutodocProgramWindow(EntityUid owner, AutodocProgram program)
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        _autodoc = _entMan.System<SharedAutodocSystem>();

        _owner = owner;
        _program = program;
        _sawmill = _logMan.GetSawmill("autodoc-ui");

        OnClose += () => _addStep?.Close();

        SafetyButton.OnPressed += _ =>
        {
            OnToggleSafety?.Invoke();
            program.SkipFailed ^= true;
            UpdateSafety();
        };
        UpdateSafety();

        RemoveButton.OnPressed += _ =>
        {
            OnRemoveProgram?.Invoke();
            Close();
        };

        AddStepButton.OnPressed += _ =>
        {
            if (_addStep is {} window)
            {
                window.MoveToFront();
                return;
            }

            _addStep = new AddStepWindow();
            _addStep.OnAddStep += step =>
            {
                // if nothing is selected add it to the end
                // if something is selected, insert just before it
                var index = _selected ?? program.Steps.Count;
                OnAddStep?.Invoke(step, index);
                _selected = null;
                RemoveButton.Disabled = true;
                program.Steps.Insert(index, step);
                UpdateSteps();
            };
            _addStep.OnClose += () => _addStep = null;
            _addStep.OpenCentered();
        };

        RemoveStepButton.OnPressed += _ =>
        {
            if (_selected is not {} index)
                return;

            _selected = null;
            RemoveStepButton.Disabled = true;
            OnRemoveStep?.Invoke(index);

            // Steps.RemoveChild throws for no fucking reason so rebuild it
            program.Steps.RemoveAt(index);
            UpdateSteps();
        };

        StartButton.OnPressed += _ =>
        {
            OnStart?.Invoke();
            Close();
        };

        ExportProgramButton.OnPressed += _ =>
        {
            ExportProgram();
        };

        Steps.OnItemSelected += args =>
        {
            _selected = args.ItemIndex;
            RemoveStepButton.Disabled = false;
        };
        Steps.OnItemDeselected += _ =>
        {
            _selected = null;
            RemoveStepButton.Disabled = true;
        };

        UpdateSteps();
        UpdateSafety();
    }

    private async void ExportProgram()
    {
        if (await _dialogManager.SaveFile(new FileDialogFilters(new FileDialogFilters.Group("yml"))) is not {} file)
            return;

        try
        {
            var node = _serMan.WriteValue(_program.GetType(), _program);
            await using var writer = new StreamWriter(file.fileStream);
            node.Write(writer);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error when exporting program: {e}");
        }
        finally
        {
            await file.fileStream.DisposeAsync();
        }
    }

    private void UpdateSafety()
    {
        var safety = !_program.SkipFailed;
        if (safety == _safety)
            return;

        _safety = safety;

        SafetyButton.Text = Loc.GetString("autodoc-safety-" + (safety ? "enabled" : "disabled"));
        if (safety)
            SafetyButton.RemoveStyleClass("Caution");
        else
            SafetyButton.AddStyleClass("Caution");
    }

    private void UpdateSteps()
    {
        var count = _program.Steps.Count;
        if (_steps == count)
            return;

        _steps = count;

        Steps.Clear();

        for (int i = 0; i < count; i++)
        {
            Steps.AddItem(_program.Steps[i].Title);
        }

        if (!_entMan.TryGetComponent<AutodocComponent>(_owner, out var comp))
            return;

        AddStepButton.Disabled = count >= comp.MaxProgramSteps;
    }

    private void UpdateStart()
    {
        if (!_entMan.TryGetComponent<AutodocComponent>(_owner, out var comp))
            return;

        StartButton.Disabled = _autodoc.GetPatient((_owner, comp)) == null;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        UpdateSteps();
        UpdateSafety();
        UpdateStart();
    }
}