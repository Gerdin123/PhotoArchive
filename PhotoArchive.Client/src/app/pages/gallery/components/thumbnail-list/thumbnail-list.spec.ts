import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ThumbnailList } from './thumbnail-list';

describe('ThumbnailList', () => {
  let component: ThumbnailList;
  let fixture: ComponentFixture<ThumbnailList>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ThumbnailList]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ThumbnailList);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
