using MoBi.Presentation.Views;
using OSPSuite.Core.Domain.Builder;
using OSPSuite.Core.Domain.Repositories;
using OSPSuite.Core.Domain.Services;
using OSPSuite.Core.Services;

namespace MoBi.Presentation.Presenter
{
   public interface IEditFavoritesInReactionsPresenter : IEditFavoritesPresenter<IBuildingBlock<IReactionBuilder>>
   {
   }

   internal class EditFavoritesInReactionsPresenter : EditFavoritesInBuildindBlockPresenter<IReactionBuilder>,
      IEditFavoritesInReactionsPresenter
   {
      public EditFavoritesInReactionsPresenter(IEditFavoritesView view, IFavoriteRepository favoriteRepository, IEntityPathResolver entityPathResolver, IEditParameterListPresenter editParameterListPresenter, IFavoriteTask favoriteTask)
         : base(view, favoriteRepository, entityPathResolver, editParameterListPresenter, favoriteTask)
      {
         UpdateSpecialColumnsVisibility = _editParameterListPresenter.ConfigureForReaction;
      }
   }
}