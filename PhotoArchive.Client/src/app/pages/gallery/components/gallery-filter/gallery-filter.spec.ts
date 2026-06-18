import { ComponentFixture, TestBed } from '@angular/core/testing';

import { GalleryFilter } from './gallery-filter';

describe('GalleryFilter', () => {
  let component: GalleryFilter;
  let fixture: ComponentFixture<GalleryFilter>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [GalleryFilter]
    })
    .compileComponents();

    fixture = TestBed.createComponent(GalleryFilter);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
