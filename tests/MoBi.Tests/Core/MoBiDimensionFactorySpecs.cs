﻿using System.Collections.Generic;
using MoBi.Core.Domain.UnitSystem;
using OSPSuite.BDDHelper;
using OSPSuite.BDDHelper.Extensions;
using OSPSuite.Core.Domain;
using OSPSuite.Core.Domain.UnitSystem;

namespace MoBi.Core
{
   public abstract class concern_for_MoBiDimensionFactory : ContextSpecification<IMoBiDimensionFactory>
   {
      protected Dimension _drugMassDimension;
      protected Dimension _volumeDimension;
      protected Dimension _flowDimension;
      protected Dimension _timeDimension;
      protected Dimension _inversedTimeDimension;
      private Dimension _anotherDimensionThatLooksLikeVolumeWithADifferentUnit;

      protected override void Context()
      {
         sut = new MoBiDimensionFactory();

         _drugMassDimension = new Dimension(new BaseDimensionRepresentation(), "DrugMass", "g");
         _volumeDimension = new Dimension(new BaseDimensionRepresentation {MassExponent = 3}, "Volume", "l");
         _anotherDimensionThatLooksLikeVolumeWithADifferentUnit = new Dimension(new BaseDimensionRepresentation {MassExponent = 3, TimeExponent = -1}, "01_OTHER", "");
         _flowDimension = new Dimension(new BaseDimensionRepresentation {MassExponent = 3, TimeExponent = -1}, "flow", "l/min");
         _timeDimension = new Dimension(new BaseDimensionRepresentation {TimeExponent = 1}, "Time", "min");
         _inversedTimeDimension = new Dimension(new BaseDimensionRepresentation {TimeExponent = -1}, "InversedTime", "1/min");

         sut.AddDimension(_drugMassDimension);
         sut.AddDimension(_volumeDimension);
         sut.AddDimension(_flowDimension);
         sut.AddDimension(_anotherDimensionThatLooksLikeVolumeWithADifferentUnit);
         sut.AddDimension(_timeDimension);
         sut.AddDimension(_inversedTimeDimension);
         sut.AddDimension(Constants.Dimension.NO_DIMENSION);
      }
   }

   public abstract class When_retrieving_dimension_from_unit_with_multiple_matching_units : concern_for_MoBiDimensionFactory
   {
      protected IDimension _result;
      protected Dimension _accelerationDimension;
      protected abstract string ConvertUnitCase(string unit);

      protected override void Context()
      {
         base.Context();
         _accelerationDimension = new Dimension(new BaseDimensionRepresentation(), "Acceleration", "G");
         sut.AddDimension(_accelerationDimension);
      }

      protected override void Because()
      {
         _result = sut.DimensionForUnit(ConvertUnitCase("g"));
      }
   }

   public class When_retrieving_upper_case_unit_from_multiple_matching_units : When_retrieving_dimension_from_unit_with_multiple_matching_units
   {
      protected override string ConvertUnitCase(string unit)
      {
         return unit.ToUpper();
      }

      [Observation]
      public void results_in_lower_case_match()
      {
         _result.ShouldBeEqualTo(_accelerationDimension);
      }
   }

   public class When_retrieving_lower_case_unit_from_multiple_matching_units : When_retrieving_dimension_from_unit_with_multiple_matching_units
   {
      protected override string ConvertUnitCase(string unit)
      {
         return unit.ToLower();
      }

      [Observation]
      public void results_in_lower_case_match()
      {
         _result.ShouldBeEqualTo(_drugMassDimension);
      }
   }

   public class When_told_to_retrieve_a_dimension_by_name_that_does_not_exist_and_that_is_not_an_RHS_dimension : concern_for_MoBiDimensionFactory
   {
      [Observation]
      public void should_throw_an_exception()
      {
         The.Action(() => sut.Dimension("TRALALA")).ShouldThrowAn<KeyNotFoundException>();
      }
   }
}