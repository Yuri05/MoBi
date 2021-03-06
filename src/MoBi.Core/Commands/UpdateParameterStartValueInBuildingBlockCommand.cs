﻿using MoBi.Assets;
using OSPSuite.Core.Commands.Core;
using MoBi.Core.Domain.Model;
using OSPSuite.Core.Domain;
using OSPSuite.Core.Domain.Builder;
using OSPSuite.Assets;

namespace MoBi.Core.Commands
{
   public class UpdateParameterStartValueInBuildingBlockCommand : BuildingBlockChangeCommandBase<IParameterStartValuesBuildingBlock>
   {
      private readonly IObjectPath _path;
      private readonly double? _value;
      private double? _originalValue;

      public UpdateParameterStartValueInBuildingBlockCommand(
         IParameterStartValuesBuildingBlock parameterStartValuesBuildingBlock, 
         IObjectPath path,
         double? value) : base(parameterStartValuesBuildingBlock)
      {
         CommandType = AppConstants.Commands.UpdateCommand;
         ObjectType = ObjectTypes.ParameterStartValue;
         _path = path;
         _value = value;
      }

      protected override void ExecuteWith(IMoBiContext context)
      {
         base.ExecuteWith(context);
         var psv = _buildingBlock[_path];
         if (psv == null) return;

         _originalValue = psv.StartValue;
         psv.StartValue = _value;
         Description = AppConstants.Commands.UpdateParameterStartValue(_path, _value, psv.DisplayUnit);
      }

      protected override IReversibleCommand<IMoBiContext> GetInverseCommand(IMoBiContext context)
      {
         return new UpdateParameterStartValueInBuildingBlockCommand(_buildingBlock, _path, _originalValue).AsInverseFor(this);
      }
   }
}