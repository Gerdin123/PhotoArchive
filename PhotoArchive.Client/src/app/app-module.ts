import { NgModule, provideBrowserGlobalErrorListeners } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';

import { AppRoutingModule } from './app-routing-module';
import { App } from './app';
import { MainPage } from './pages/main/main-page/main-page';
import { Navbar } from './shared/navbar/navbar';
import { GalleryPage } from './pages/gallery/gallery-page/gallery-page';
import { ThumbnailList } from './pages/gallery/components/thumbnail-list/thumbnail-list';
import { GalleryFilter } from './pages/gallery/components/gallery-filter/gallery-filter';
import { ImageDetailsPage } from './pages/image-details/image-details-page/image-details-page';
import { ImageMetadata } from './pages/image-details/components/image-metadata/image-metadata';
import { TagManager } from './pages/image-details/components/tag-manager/tag-manager';
import { PeopleManager } from './pages/image-details/components/people-manager/people-manager';
import { EditImagePage } from './pages/edit-image/edit-image-page/edit-image-page';
import { ImageEditForm } from './pages/edit-image/components/image-edit-form/image-edit-form';
import { ThumbnailCard } from './pages/gallery/components/thumbnail-card/thumbnail-card';

@NgModule({
  declarations: [
    App,
    ThumbnailCard,
    MainPage,
    Navbar,
    GalleryPage,
    ThumbnailList,
    GalleryFilter,
    ImageDetailsPage,
    ImageMetadata,
    TagManager,
    PeopleManager,
    EditImagePage,
    ImageEditForm,
    ThumbnailCard
  ],
  imports: [
    BrowserModule,
    AppRoutingModule
  ],
  providers: [
    provideBrowserGlobalErrorListeners(),
  ],
  bootstrap: [App]
})
export class AppModule { }
