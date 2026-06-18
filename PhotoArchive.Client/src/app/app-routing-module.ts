import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { GalleryPage } from './pages/gallery/gallery-page/gallery-page';
import { MainPage } from './pages/main/main-page/main-page';
import { ImageDetailsPage } from './pages/image-details/image-details-page/image-details-page';
import { EditImagePage } from './pages/edit-image/edit-image-page/edit-image-page';

const routes: Routes = [
  { path: '', component: MainPage },
  { path: 'gallery', component: GalleryPage },
  { path: 'image/:id', component: ImageDetailsPage},
  { path: 'image/:id/edit', component: EditImagePage},
  { path: '**', redirectTo: '' },
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
