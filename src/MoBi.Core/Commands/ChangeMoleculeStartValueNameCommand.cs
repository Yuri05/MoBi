﻿using OSPSuite.Core.Commands.Core;
using MoBi.Core.Domain.Model;
using OSPSuite.Core.Domain;
using OSPSuite.Core.Domain.Builder;

namespace MoBi.Core.Commands
{
   public class ChangeMoleculeStartValueNameCommand : ChangeStartValueNameCommand<IMoleculeStartValuesBuildingBlock, IMoleculeStartValue>
   {
      public ChangeMoleculeStartValueNameCommand(IMoleculeStartValuesBuildingBlock buildingBlock, IObjectPath path, string newValue): base(buildingBlock, path, newValue)
      {
      }

      protected override IReversibleCommand<IMoBiContext> GetInverseCommand(IMoBiContext context)
      {
         return new ChangeMoleculeStartValueNameCommand(_buildingBlock, new ObjectPath(_path), _oldValue).AsInverseFor(this);
      }
   }
}