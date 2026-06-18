import { Image } from "./image";

export interface ImageDetail extends Image{
    createdAt: Date;
    updatedAt: Date;
}