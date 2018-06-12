﻿using MoBi.Presentation.DTO;
using OSPSuite.Core.Domain;
using OSPSuite.Core.Domain.Repositories;
using OSPSuite.Core.Domain.Services;
using OSPSuite.Presentation.DTO;
using OSPSuite.Presentation.Mappers;
using OSPSuite.Utility.Extensions;

namespace MoBi.Presentation.Mappers
{
   public interface IParameterToParameterDTOMapper : OSPSuite.Presentation.Mappers.IParameterToParameterDTOMapper
   {
   }

   public class ParameterToParameterDTOMapper : ObjectBaseToObjectBaseDTOMapperBase, IParameterToParameterDTOMapper
   {
      private readonly IFormulaToFormulaBuilderDTOMapper _formulaToDTOFormulaBuilderMapper;
      private readonly ITagToTagDTOMapper _tagMapper;
      private readonly IGroupRepository _groupRepository;
      private readonly IFavoriteRepository _favoriteRepository;
      private readonly IEntityPathResolver _entityPathResolver;
      private readonly IPathToPathElementsMapper _pathToPathElementsMapper;

      public ParameterToParameterDTOMapper(IFormulaToFormulaBuilderDTOMapper formulaToDTOFormulaBuilderMapper, 
         ITagToTagDTOMapper tagMapper, 
         IGroupRepository groupRepository, 
         IFavoriteRepository favoriteRepository, 
         IEntityPathResolver entityPathResolver, 
         IPathToPathElementsMapper pathToPathElementsMapper)
      {
         _formulaToDTOFormulaBuilderMapper = formulaToDTOFormulaBuilderMapper;
         _tagMapper = tagMapper;
         _groupRepository = groupRepository;
         _favoriteRepository = favoriteRepository;
         _entityPathResolver = entityPathResolver;
         _pathToPathElementsMapper = pathToPathElementsMapper;
      }

      public IParameterDTO MapFrom(IParameter parameter)
      {
         var dto = new ParameterDTO(parameter);
         MapProperties(parameter, dto);
         dto.Formula = _formulaToDTOFormulaBuilderMapper.MapFrom(parameter.Formula);
         dto.RHSFormula = _formulaToDTOFormulaBuilderMapper.MapFrom(parameter.RHSFormula);
         dto.BuildMode = parameter.BuildMode;
         dto.Dimension = parameter.Dimension;
         dto.HasRHS = (parameter.RHSFormula != null);
         dto.DisplayUnit = parameter.Dimension.BaseUnit;
         dto.Group = _groupRepository.GroupByName(parameter.GroupName);
         dto.IsAdvancedParameter = !parameter.Visible;
         dto.CanBeVariedInPopulation = parameter.CanBeVariedInPopulation;
         dto.PathElements = _pathToPathElementsMapper.MapFrom(parameter);
         dto.Tags = parameter.Tags.MapAllUsing(_tagMapper).ToRichList();
         var parameterPath = _entityPathResolver.ObjectPathFor(parameter);
         dto.IsFavorite = _favoriteRepository.Contains(parameterPath);

         return dto;
      }
   }
}