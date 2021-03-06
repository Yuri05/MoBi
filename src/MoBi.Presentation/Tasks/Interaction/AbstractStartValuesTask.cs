using System;
using System.Collections.Generic;
using System.Linq;
using MoBi.Assets;
using MoBi.Core.Commands;
using MoBi.Core.Domain.Extensions;
using MoBi.Core.Domain.Model;
using MoBi.Core.Domain.Services;
using MoBi.Core.Exceptions;
using MoBi.Presentation.DTO;
using MoBi.Presentation.Presenter;
using MoBi.Presentation.Tasks.Edit;
using OSPSuite.Core.Commands;
using OSPSuite.Core.Commands.Core;
using OSPSuite.Core.Domain;
using OSPSuite.Core.Domain.Builder;
using OSPSuite.Core.Domain.Formulas;
using OSPSuite.Core.Domain.Services;
using OSPSuite.Core.Domain.UnitSystem;
using OSPSuite.Utility;
using OSPSuite.Utility.Collections;
using OSPSuite.Utility.Extensions;

namespace MoBi.Presentation.Tasks.Interaction
{
   public abstract class AbstractStartValuesTask<TBuildingBlock, TStartValue> : InteractionTasksForEnumerableBuildingBlock<TBuildingBlock, TStartValue>, IStartValuesTask<TBuildingBlock, TStartValue>
      where TBuildingBlock : class, IBuildingBlock, IStartValuesBuildingBlock<TStartValue>
      where TStartValue : class, IStartValue
   {
      protected IIgnoreReplaceMergeManager<TStartValue> _startValueBuildingBlockMergeManager;
      protected readonly ICloneManagerForBuildingBlock _cloneManagerForBuildingBlock;
      private readonly IMoBiFormulaTask _moBiFormulaTask;
      private readonly ISpatialStructureFactory _spatialStructureFactory;
      private readonly IMapper<ImportedQuantityDTO, TStartValue> _dtoToQuantityToParameterStartValueMapper;
      private readonly IStartValuePathTask<TBuildingBlock, TStartValue> _startValuePathTask;

      protected AbstractStartValuesTask(IInteractionTaskContext interactionTaskContext, IEditTasksForBuildingBlock<TBuildingBlock> editTask,
         IIgnoreReplaceMergeManager<TStartValue> startValueBuildingBlockMergeManager, ICloneManagerForBuildingBlock cloneManagerForBuildingBlock,
         IMoBiFormulaTask moBiFormulaTask, ISpatialStructureFactory spatialStructureFactory, IMapper<ImportedQuantityDTO, TStartValue> dtoToQuantityToParameterStartValueMapper,
         IStartValuePathTask<TBuildingBlock, TStartValue> startValuePathTask)
         : base(interactionTaskContext, editTask)
      {
         _startValueBuildingBlockMergeManager = startValueBuildingBlockMergeManager;
         _cloneManagerForBuildingBlock = cloneManagerForBuildingBlock;
         _moBiFormulaTask = moBiFormulaTask;
         _spatialStructureFactory = spatialStructureFactory;
         _dtoToQuantityToParameterStartValueMapper = dtoToQuantityToParameterStartValueMapper;
         _startValuePathTask = startValuePathTask;
      }

      public override IMoBiCommand AddNew(IMoBiProject project, IBuildingBlock buildingBlockToAddTo)
      {
         if (!project.MoleculeBlockCollection.Any() || !project.SpatialStructureCollection.Any())
            throw new MoBiException(AppConstants.Exceptions.UnableToCreateStartValues);

         TBuildingBlock newEntity;
         using (var createPresenter = ApplicationController.Start<ICreateStartValuesPresenter<TBuildingBlock>>())
         {
            newEntity = createPresenter.Create();
         }

         if (newEntity == null)
            return new MoBiEmptyCommand();

         var macroCommand = new MoBiMacroCommand
         {
            ObjectType = ObjectName,
            CommandType = AppConstants.Commands.AddCommand
         };
         macroCommand.Add(GetAddCommand(newEntity, project, buildingBlockToAddTo).Run(Context));

         //Icon may depend on name. 
         newEntity.Icon = InteractionTask.IconFor(newEntity);
         macroCommand.Description = AppConstants.Commands.AddToProjectDescription(ObjectName, newEntity.Name);
         _editTask.EditBuildingBlock(newEntity);
         return macroCommand;
      }

      /// <summary>
      ///    Updates the start values defined in <paramref name="startValuesToUpdate" /> with the values defined in
      ///    <paramref name="startValueInfo" />. Returns a template cache containg all values defined in the template
      /// </summary>
      public ICache<string, TStartValue> UpdateValuesFromTemplate(TBuildingBlock startValuesToUpdate, IBuildingBlockInfo<TBuildingBlock> startValueInfo)
      {
         var templateStartValues = startValueInfo.BuildingBlock;
         if (startValueInfo.BuildingBlockIsTemplate)
            templateStartValues = startValueInfo.TemplateBuildingBlock;

         var startValueCache = startValuesToUpdate.ToCache();
         var templateCache = templateStartValues.ToCache();

         _cloneManagerForBuildingBlock.FormulaCache = startValuesToUpdate.FormulaCache;

         try
         {
            foreach (var templateKeyValue in templateCache.KeyValues)
            {
               var startValue = startValueCache[templateKeyValue.Key];

               if (startValue == null)
                  addStartValueToCache(startValuesToUpdate, templateKeyValue.Value);
               else
                  startValue.UpdatePropertiesFrom(templateKeyValue.Value, _cloneManagerForBuildingBlock);
            }
            startValuesToUpdate.Version = templateStartValues.Version;
         }
         finally
         {
            _cloneManagerForBuildingBlock.FormulaCache = null;
         }
         return templateCache;
      }

      private void addStartValueToCache(TBuildingBlock startValueBuildingBlock, TStartValue startValue)
      {
         var cloneStartValue = _cloneManagerForBuildingBlock.Clone(startValue, startValueBuildingBlock.FormulaCache);
         startValueBuildingBlock.Add(cloneStartValue);
      }

      /// <summary>
      ///    Returns the default dimension for the start value type
      /// </summary>
      /// <returns>The default dimension</returns>
      public abstract IDimension GetDefaultDimension();

      protected T BuildingBlockById<T>(string buildingBlockId) where T : class, IBuildingBlock
      {
         if (!Context.ObjectRepository.ContainsObjectWithId(buildingBlockId))
            throw new MoBiException(AppConstants.Exceptions.SourceBuildingBlockNotInProject);

         return Context.Get<T>(buildingBlockId);
      }

      public IEnumerable<string> GetContainerPathItemsForBuildingBlock(TBuildingBlock buildingBlock)
      {
         var spatialStructure = SpatialStructureReferencedBy(buildingBlock);
         var moleculeBuildingBlock = MoleculeBuildingBlockReferencedBy(buildingBlock);
         var nameList = new List<string>();

         spatialStructure.Each(container => container.GetAllContainersAndSelf<IContainer>().Each(x => nameList.Add(x.Name)));
         moleculeBuildingBlock.Each(builder => nameList.Add(builder.Name));

         return nameList.Distinct();
      }

      public abstract bool IsEquivalentToOriginal(TStartValue startValue, TBuildingBlock buildingBlock);

      protected ISpatialStructure SpatialStructureReferencedBy(TBuildingBlock buildingBlock)
      {
         return Context.Get<ISpatialStructure>(buildingBlock.SpatialStructureId) ?? _spatialStructureFactory.Create();
      }

      protected IMoleculeBuildingBlock MoleculeBuildingBlockReferencedBy(TBuildingBlock buildingBlock)
      {
         return Context.Get<IMoleculeBuildingBlock>(buildingBlock.MoleculeBuildingBlockId) ?? Context.Create<IMoleculeBuildingBlock>(buildingBlock.MoleculeBuildingBlockId);
      }

      /// <summary>
      ///    Checks that the formula is equivalent for the start value. This includes evaluation of constant formula to a double
      /// </summary>
      /// <param name="startValue">The start value to check</param>
      /// <param name="targetFormula">The formula being evaluated</param>
      /// <returns>True if the formula is equivalent to the start value formula</returns>
      protected bool HasEquivalentFormula(IStartValue startValue, IFormula targetFormula)
      {
         return _startValuePathTask.HasEquivalentFormula(startValue, targetFormula);
      }

      protected static bool HasEquivalentStartValue(IStartValue startValue, double? originalStartValue)
      {
         if (!originalStartValue.HasValue)
            return double.IsNaN(startValue.StartValue.GetValueOrDefault(Double.NaN));

         if (!startValue.StartValue.HasValue)
            return false;

         return (ValueComparer.AreValuesEqual(originalStartValue.Value, startValue.StartValue.Value));
      }

      protected static bool HasEquivalentDimension(IWithDimension subject, IWithDimension target)
      {
         return target.Dimension == subject.Dimension;
      }

      private MoBiMacroCommand createExtendMacroCommand(TBuildingBlock buildingBlockToExtend)
      {
         var moBiMacroCommand = new BulkUpdateMacroCommand
         {
            CommandType = AppConstants.Commands.ExtendCommand,
            Description = AppConstants.Commands.ExtendDescription,
            ObjectType = _interactionTaskContext.GetTypeFor(buildingBlockToExtend)
         };
         return moBiMacroCommand;
      }

      protected IMoBiCommand Extend(TBuildingBlock newStartValues, TBuildingBlock buildingBlockToExtend)
      {
         var macro = createExtendMacroCommand(buildingBlockToExtend);

         prepareMergeActions(buildingBlockToExtend, macro);

         var cacheToMerge = newStartValues.ToCache();
         var targetCache = buildingBlockToExtend.ToCache();

         // Use the merge manager to implement the extend. We can take advantage of the equivalency checker to favour the existing 
         // start value if a conflict is found (always prefer the existing start value)
         _startValueBuildingBlockMergeManager.Merge(cacheToMerge, targetCache, areElementsEquivalent: (s1, s2) => true);

         macro.Run(Context);

         return macro;
      }

      public override IMoBiCommand Merge(TBuildingBlock buildingBlockToMerge, TBuildingBlock targetBuildingBlock)
      {
         if (targetBuildingBlock == null)
            return AddToProject(buildingBlockToMerge);

         var macro = CreateMergeMacroCommand(targetBuildingBlock);

         prepareMergeActions(targetBuildingBlock, macro);

         var cacheToMerge = buildingBlockToMerge.ToCache();
         var targetCache = targetBuildingBlock.ToCache();

         _startValueBuildingBlockMergeManager.Merge(cacheToMerge, targetCache, AreEquivalentItems);
         macro.Run(Context);

         return macro;
      }

      private void prepareMergeActions(TBuildingBlock targetBuildingBlock, MoBiMacroCommand macro)
      {
         _startValueBuildingBlockMergeManager.AddAction = startValueToMerge => macro.Add(GenerateAddCommandAndUpdateFormulaReferences(startValueToMerge, targetBuildingBlock));
         _startValueBuildingBlockMergeManager.RemoveAction = startValueToMerge => macro.Add(GenerateRemoveCommand(targetBuildingBlock, startValueToMerge));
         _startValueBuildingBlockMergeManager.CancelAction = macro.Clear;
      }

      protected override IMoBiMacroCommand GenerateAddCommandAndUpdateFormulaReferences(TStartValue startValueToMerge, TBuildingBlock targetBuildingBlock, string originalBuilderName = null)
      {
         var macroCommand = CreateAddBuilderMacroCommand(startValueToMerge, targetBuildingBlock);

         macroCommand.Add(GenerateAddCommand(targetBuildingBlock, startValueToMerge));
         macroCommand.Add(_moBiFormulaTask.AddFormulaToCacheOrFixReferenceCommand(targetBuildingBlock, startValueToMerge));

         return macroCommand;
      }

      protected abstract bool AreEquivalentItems(TStartValue first, TStartValue second);
      protected abstract IMoBiCommand GenerateRemoveCommand(TBuildingBlock targetBuildingBlock, TStartValue startValueToRemove);
      protected abstract IMoBiCommand GenerateAddCommand(TBuildingBlock targetBuildingBlock, TStartValue startValueToAdd);
      public abstract void ExtendStartValues(TBuildingBlock startValuesBuildingBlock);
      public abstract TBuildingBlock CreateStartValuesForSimulation(IMoBiBuildConfiguration buildConfiguration);
      public abstract IMoBiCommand AddStartValueToBuildingBlock(TBuildingBlock buildingBlock, TStartValue startValue);
      public abstract IMoBiCommand ImportStartValuesToBuildingBlock(TBuildingBlock startValuesBuildingBlock, IEnumerable<ImportedQuantityDTO> startValues);
      public abstract IMoBiCommand RemoveStartValueFromBuildingBlockCommand(TStartValue startValue, TBuildingBlock buildingBlock);
      public abstract IMoBiCommand RefreshStartValuesFromBuildingBlocks(TBuildingBlock buildingBlock, IEnumerable<TStartValue> startValuesToRefresh);

      public IMoBiCommand ChangeStartValueFormulaCommand(TBuildingBlock startValues, TStartValue startValue, IFormula formula)
      {
         return new ChangeStartValueFormulaCommand<TStartValue>(startValues, startValue, formula, startValue.Formula).Run(Context);
      }

      public IMoBiCommand SetStartValueWithUnit(TStartValue startValue, double? newBaseValue, Unit unit, TBuildingBlock startValues)
      {
         return new StartValueValueOrUnitChangedCommand<TStartValue, TBuildingBlock>(startValue, newBaseValue, unit, startValues).Run(Context);
      }

      public IMoBiCommand SetStartDisplayValueWithUnit(TStartValue startValue, double? newDisplayValue, Unit unit, TBuildingBlock startValues)
      {
         return SetStartValueWithUnit(startValue, unit.UnitValueToBaseUnitValue(newDisplayValue.GetValueOrDefault(double.NaN)), unit, startValues);
      }

      public IMoBiCommand UpdateStartValueDimension(TBuildingBlock startValuesBuildingBlock, TStartValue startValue, IDimension newDimension)
      {
         return new UpdateDimensionInStartValueCommand<TStartValue>(startValue, newDimension, _interactionTaskContext.DisplayUnitFor(newDimension), startValuesBuildingBlock).Run(Context);
      }

      public ICommand SetValueOrigin(TBuildingBlock buildingBlock, ValueOrigin valueOrigin, TStartValue startValue)
      {
         return new UpdateValueOriginInStartValueCommand<TStartValue>(startValue, valueOrigin, buildingBlock).Run(Context);
      }

      public IMoBiCommand SetUnit(TBuildingBlock buildingBlock, TStartValue startValue, Unit newUnit)
      {
         return setValue(startValue, startValue.ConvertToDisplayUnit(startValue.StartValue), newUnit, buildingBlock);
      }

      public IMoBiCommand SetValue(TBuildingBlock buildingBlock, double? valueInDisplayUnit, TStartValue startValue)
      {
         var macroCommand = new MoBiMacroCommand
         {
            CommandType = AppConstants.Commands.EditCommand,
            ObjectType = _interactionTaskContext.GetTypeFor<TStartValue>(),
            Description = AppConstants.Commands.SetStartValueAndFormula
         };

         macroCommand.Add(setValue(startValue, valueInDisplayUnit, startValue.DisplayUnit, buildingBlock));
         if (startValue.Formula != null)
            macroCommand.Add(setFormula(buildingBlock, startValue, null));
         return macroCommand;
      }

      private IMoBiCommand setValue(TStartValue startValue, double? newDisplayValue, Unit unit, TBuildingBlock buildingBlock)
      {
         return SetStartDisplayValueWithUnit(startValue, newDisplayValue, unit, buildingBlock);
      }

      public IMoBiCommand SetFormula(TBuildingBlock buildingBlock, TStartValue startValue, IFormula formula)
      {
         var macroCommand = new MoBiMacroCommand
         {
            CommandType = AppConstants.Commands.EditCommand,
            ObjectType = _interactionTaskContext.GetTypeFor<TStartValue>(),
            Description = AppConstants.Commands.SetStartValueAndFormula
         };

         macroCommand.Add(setFormula(buildingBlock, startValue, formula));
         if (startValue.StartValue.HasValue)
            macroCommand.Add(setValue(startValue, null, startValue.DisplayUnit, buildingBlock));

         return macroCommand;
      }

      public abstract bool CanResolve(TBuildingBlock buildingBlock, TStartValue startValue);

      private IMoBiCommand setFormula(TBuildingBlock buildingBlock, TStartValue startValue, IFormula formula)
      {
         return ChangeStartValueFormulaCommand(buildingBlock, startValue, formula);
      }

      protected IMoBiCommand AddFormulaToFormulaCacheAndSetOnStartValue<TFormula>(TBuildingBlock startValuesBuildingBlock, TStartValue startValue, IParameter referenceParameter)
         where TFormula : IFormula
      {
         var macroCommand = new MoBiMacroCommand
         {
            CommandType = AppConstants.Commands.AddCommand,
            Description = AppConstants.Commands.AddFormulaToBuildingBlock,
            ObjectType = _interactionTaskContext.GetTypeFor<TFormula>()
         };

         var newFormula = _moBiFormulaTask.CreateNewFormula<TFormula>(startValue.Dimension);

         macroCommand.AddCommand(new AddFormulaToFormulaCacheCommand(startValuesBuildingBlock, newFormula).Run(Context));

         if (!_moBiFormulaTask.EditNewFormula(newFormula, macroCommand, startValuesBuildingBlock, referenceParameter))
            return CancelCommand(macroCommand);

         macroCommand.Add(SetFormula(startValuesBuildingBlock, startValue, newFormula));
         return macroCommand;
      }

      protected static bool ShouldFormulaBeOverridden(ImportedQuantityDTO quantityDTO, TStartValue startValue)
      {
         return quantityDTO.IsQuantitySpecified && startValue.Formula != null;
      }

      protected IMoBiCommand GetChangeStartValueFormulaCommand(TBuildingBlock startValuesBuildingBlock, TStartValue startValue, IFormula newFormula, IFormula oldFormula)
      {
         return new ChangeStartValueFormulaCommand<TStartValue>(startValuesBuildingBlock, startValue, newFormula, oldFormula);
      }

      protected abstract IMoBiCommand GetUpdateStartValueInBuildingBlockCommand(TBuildingBlock startValuesBuildingBlock, ImportedQuantityDTO dto);

      protected void GetImportStartValuesMacroCommand(TBuildingBlock startValuesBuildingBlock, IEnumerable<ImportedQuantityDTO> startValues, BulkUpdateMacroCommand macroCommand)
      {
         startValues.Each(startValueDTO =>
         {
            var startValue = startValuesBuildingBlock[startValueDTO.Path];

            if (startValue == null)
               macroCommand.Add(GenerateAddCommand(startValuesBuildingBlock, _dtoToQuantityToParameterStartValueMapper.MapFrom(startValueDTO)));
            else
            {
               if (ShouldFormulaBeOverridden(startValueDTO, startValue))
                  macroCommand.Add(GetChangeStartValueFormulaCommand(startValuesBuildingBlock, startValue: startValue, newFormula: null, oldFormula: startValue.Formula));

               macroCommand.Add(GetUpdateStartValueInBuildingBlockCommand(startValuesBuildingBlock, startValueDTO));
            }
         });
      }

      public IMoBiCommand EditStartValueName(TBuildingBlock startValues, TStartValue startValue, string newValue)
      {
         return _startValuePathTask.UpdateStartValueName(startValues, startValue, newValue);
      }

      public IMoBiCommand EditStartValueContainerPath(TBuildingBlock buildingBlock, TStartValue startValue, int indexToUpdate, string newValue)
      {
         return _startValuePathTask.UpdateStartValueContainerPath(buildingBlock, startValue, indexToUpdate, newValue);
      }
   }
}