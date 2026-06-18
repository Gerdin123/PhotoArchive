import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ImageDetailsPage } from './image-details-page';

describe('ImageDetailsPage', () => {
  let component: ImageDetailsPage;
  let fixture: ComponentFixture<ImageDetailsPage>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ImageDetailsPage]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ImageDetailsPage);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
