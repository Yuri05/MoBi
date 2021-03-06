﻿using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using MoBi.Core.Domain.Model;
using MoBi.Core.Events;
using MoBi.Presentation.DTO;
using MoBi.Presentation.Mappers;
using MoBi.Presentation.Nodes;
using MoBi.Presentation.Views;
using OSPSuite.Assets;
using OSPSuite.Core.Domain;
using OSPSuite.Core.Domain.Builder;
using OSPSuite.Presentation.Core;
using OSPSuite.Presentation.Presenters;
using OSPSuite.Presentation.Presenters.ContextMenus;
using OSPSuite.Utility.Events;
using OSPSuite.Utility.Extensions;

namespace MoBi.Presentation.Presenter
{
   public interface IHierarchicalSpatialStructurePresenter : IEditPresenter<ISpatialStructure>,
      IHierarchicalStructurePresenter,
      IListener<AddedEvent>, 
      IListener<RemovedEvent>, 
      IListener<EntitySelectedEvent>
   {
      void Select(IEntity entity);
   }

   public class HierarchicalSpatialStructurePresenter : HierarchicalStructurePresenter,
      IHierarchicalSpatialStructurePresenter
   {
      private ISpatialStructure _spatialStructure;
      private readonly IViewItemContextMenuFactory _contextMenuFactory;

      public HierarchicalSpatialStructurePresenter(IHierarchicalStructureView view, IMoBiContext context,
         IObjectBaseToObjectBaseDTOMapper objectBaseMapper, IViewItemContextMenuFactory contextMenuFactory, ITreeNodeFactory treeNodeFactory)
         : base(view, context, objectBaseMapper, treeNodeFactory)
      {
         _contextMenuFactory = contextMenuFactory;
      }

      public void InitializeWith(ISpatialStructure spatialStructure)
      {
         _spatialStructure = spatialStructure;

         _view.AddNode(_favoritesNode);
         _view.AddNode(_userDefinedNode);

         var roots = new List<IObjectBaseDTO> {_objectBaseMapper.MapFrom(spatialStructure.GlobalMoleculeDependentProperties)};
         spatialStructure.TopContainers.Each(x => roots.Add(_objectBaseMapper.MapFrom(x)));

         var neighborhood = _objectBaseMapper.MapFrom(spatialStructure.NeighborhoodsContainer);
         neighborhood.Description = ToolTips.BuildingBlockSpatialStructure.HowToCreateNeighborhood;

         roots.Add(neighborhood);

         _view.Show(roots);
      }

      public void Edit(ISpatialStructure objectToEdit)
      {
         InitializeWith(objectToEdit);
      }

      public object Subject => _spatialStructure;

      public void Edit(object objectToEdit)
      {
         Edit(objectToEdit.DowncastTo<ISpatialStructure>());
      }

      protected override void RaiseFavoritesSelectedEvent()
      {
         _context.PublishEvent(new FavoritesSelectedEvent(_spatialStructure));
      }

      protected override void RaiseUserDefinedSelectedEvent()
      {
         _context.PublishEvent(new UserDefinedSelectedEvent(_spatialStructure));
      }

      public override void ShowContextMenu(IViewItem objectRequestingPopup, Point popupLocation)
      {
         var contextMenu = _contextMenuFactory.CreateFor(objectRequestingPopup ?? new SpatialStructureRootItem(), this);
         contextMenu.Show(_view, popupLocation);
      }

      public void Handle(AddedEvent eventToHandle)
      {
         if (_spatialStructure == null) return;
         var entity = eventToHandle.AddedObject as IContainer;

         if (entity.IsAnImplementationOf<IDistributedParameter>()) return;
         if (entity == null) return;

         var dto = _objectBaseMapper.MapFrom(entity);
         if (_spatialStructure.Any(tc => tc.GetAllContainersAndSelf<IContainer>().Contains(entity.ParentContainer)))
         {
            _view.Add(dto, _objectBaseMapper.MapFrom(entity.ParentContainer));
         }
         else
         {
            if (eventToHandle.Parent != _spatialStructure) return;
            _view.AddRoot(dto);
         }
      }

      public void Handle(RemovedEvent eventToHandle)
      {
         if (_spatialStructure == null) return;

         foreach (var objectBase in eventToHandle.RemovedObjects.OfType<IEntity>())
         {
            _view.Remove(_objectBaseMapper.MapFrom(objectBase));
         }
      }

      public void Select(IEntity entity)
      {
         _view.Select(entity.Id);
      }

      public void Handle(EntitySelectedEvent eventToHandle)
      {
         if (eventToHandle.Sender == this) return;
         var entity = eventToHandle.ObjectBase as IEntity;
         if (entity == null || _spatialStructure == null) return;
         if (!_spatialStructure.TopContainers.Contains(entity.RootContainer)) return;

         var entityToSelect = entity.IsAnImplementationOf<IParameter>() ? entity.ParentContainer : entity;
         _view.Select(entityToSelect.Id);
      }
   }
}