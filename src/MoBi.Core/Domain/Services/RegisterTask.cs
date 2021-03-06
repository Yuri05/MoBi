﻿using OSPSuite.Utility.Extensions;
using MoBi.Core.Domain.Model;
using OSPSuite.Core.Domain;
using OSPSuite.Core.Domain.Formulas;

namespace MoBi.Core.Domain.Services
{
   public interface IRegisterTask
   {
      /// <summary>
      ///    Registers all objects defined in the given <paramref name="objectToRegister" />
      /// </summary>
      /// <param name="objectToRegister"></param>
      void RegisterAllIn(IWithId objectToRegister);

      /// <summary>
      ///    Registers all objects defined in the <paramref name="project" /> but the simulations
      /// </summary>
      void Register(IMoBiProject project);
   }

   public class RegisterTask : AbstractRegistrationTask, IRegisterTask
   {
      public RegisterTask(IWithIdRepository withIdRepository) : base(withIdRepository)
      {
      }

      public override void Visit(IWithId objectBase)
      {
         register(objectBase);
      }

      private void register(IWithId objectBase)
      {
         if (objectBase == null) return;
         _withIdRepository.Register(objectBase);
      }

      protected override void Visit(IFormula formula)
      {
         register(formula);
      }

      public void RegisterAllIn(IWithId objectToRegister)
      {
         //If already registered, no need to register the whole hiearchy again
         if (_withIdRepository.ContainsObjectWithId(objectToRegister.Id))
            return;

         var objectBase = objectToRegister as IObjectBase;
         if (objectBase == null)
            Visit(objectToRegister);
         else
            objectBase.AcceptVisitor(this);
      }

      public void Register(IMoBiProject project)
      {
         register(project);
         project.AllBuildingBlocks().Each(RegisterAllIn);
         project.AllObservedData.Each(RegisterAllIn);
      }
   }
}